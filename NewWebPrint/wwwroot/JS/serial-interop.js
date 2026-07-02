// wwwroot/js/serial-interop.js

let port;
let reader;
let keepReading = false;
let readableStreamClosed;

/** 称重串口常用波特率（与现场仪表约定）；首次默认 19200，与历史页面硬编码保持一致 */
const SCALE_BAUD_STORAGE_KEY = 'blazorScaleSerial.baudRate';
const DEFAULT_SCALE_BAUD = 19200;

window.serialInterop = {
    /** 读取本机记住的称重波特率（9600/19200 等），供各页面打开串口前使用 */
    getPreferredScaleBaud: function () {
        try {
            const raw = localStorage.getItem(SCALE_BAUD_STORAGE_KEY);
            if (raw != null && raw !== '') {
                const n = parseInt(raw, 10);
                if (n > 0) return String(n);
            }
        } catch (_) { }
        return String(DEFAULT_SCALE_BAUD);
    },

    /** 记住称重波特率（打开串口成功后也会自动调用） */
    setPreferredScaleBaud: function (baudRateStr) {
        try {
            const n = parseInt(baudRateStr, 10);
            if (n > 0) localStorage.setItem(SCALE_BAUD_STORAGE_KEY, String(n));
        } catch (_) { }
    },

    /** 调试日志（页面可实现 JSInvokable OnScaleBaudProbe） */
    _notifyScaleBaudProbe: async function (dotNetHelper, message) {
        if (!dotNetHelper || !message) return;
        try {
            await dotNetHelper.invokeMethodAsync('OnScaleBaudProbe', message);
        } catch (_) { /* 页面未实现时忽略 */ }
    },

    /**
     * 短时读取若干字节用于波特率探测（不启动正式 readLoop）
     */
    _readProbeSample: async function (serialPort, durationMs, maxBytes) {
        if (!serialPort.readable) return new Uint8Array(0);
        const reader = serialPort.readable.getReader();
        const parts = [];
        let total = 0;
        const deadline = Date.now() + durationMs;
        try {
            while (Date.now() < deadline && total < maxBytes) {
                const timeLeft = deadline - Date.now();
                if (timeLeft <= 0) break;
                const slot = Math.min(120, timeLeft);
                const chunk = await Promise.race([
                    reader.read(),
                    new Promise((resolve) => setTimeout(() => resolve({ __tick: true }), slot))
                ]);
                if (chunk && chunk.__tick) continue;
                const { value, done } = chunk || {};
                if (done) break;
                if (value && value.length) {
                    parts.push(value);
                    total += value.length;
                }
            }
        } finally {
            try { await reader.cancel(); } catch (_) { }
            try { reader.releaseLock(); } catch (_) { }
        }
        if (parts.length === 0) return new Uint8Array(0);
        const out = new Uint8Array(total);
        let o = 0;
        for (const p of parts) {
            out.set(p, o);
            o += p.length;
        }
        return out;
    },

    /**
     * 是否应在 9600/19200 之间换另一种波特率重试（样本像乱码时返回 true）
     * 样本过少或几乎无数据时不判断为乱码，避免误切。
     */
    _shouldRetryOtherBaud: function (bytes) {
        if (!bytes || bytes.length === 0) return false;
        if (bytes.length < 8) return false;

        let okChars = 0;
        let high = 0;
        for (let i = 0; i < bytes.length; i++) {
            const b = bytes[i];
            if (b >= 0x20 && b <= 0x7e) okChars++;
            else if (b === 0x0d || b === 0x0a || b === 0x09) okChars++;
            if (b > 0x7f) high++;
        }
        const ratio = okChars / bytes.length;
        const highRatio = high / bytes.length;

        const asciiStr = this._bytesToAscii(bytes);
        const w = this._extractWeight(asciiStr);

        if (ratio >= 0.72) return false;
        if (w && ratio >= 0.5) return false;
        if (highRatio > 0.35 && bytes.length >= 10) return true;
        if (ratio < 0.42 && bytes.length >= 12) return true;
        if (ratio < 0.55 && bytes.length >= 20 && !w) return true;
        return false;
    },

    // 强制清理旧连接，避免“上一次异常断开”影响本次打开
    _safeCleanup: async function () {
        keepReading = false;
        try {
            if (reader) {
                try { await reader.cancel(); } catch (_) { }
                try { reader.releaseLock(); } catch (_) { }
                reader = undefined;
            }
            if (readableStreamClosed) {
                try { await readableStreamClosed; } catch (_) { }
                readableStreamClosed = undefined;
            }
            if (port) {
                try { await port.close(); } catch (_) { }
                port = undefined;
            }
        } catch (_) { }
    },

    // 检测浏览器是否支持 Serial API
    isSerialSupported: function () {
        return 'serial' in navigator;
    },

    // 获取浏览器名称
    getBrowserName: function () {
        const userAgent = navigator.userAgent;
        if (userAgent.indexOf("Edg") > -1) return "edge";
        if (userAgent.indexOf("Chrome") > -1) return "chrome";
        if (userAgent.indexOf("Firefox") > -1) return "firefox";
        if (userAgent.indexOf("Safari") > -1) return "safari";
        return "unknown";
    },

    // 检查串口权限状态（Chrome/Edge 特有）
    checkPermission: async function (vendorId, productId) {
        if (!this.isSerialSupported()) return "unsupported";

        try {
            const devices = await navigator.serial.getPorts();
            if (devices.length > 0) {
                // 检查是否已有权限
                const filters = [];
                if (vendorId) filters.push({ vendorId });
                if (productId) filters.push({ productId });

                const permission = await navigator.serial.requestPort({ filters });
                return "granted";
            }
            return "prompt";
        } catch (error) {
            return "denied";
        }
    },

    // 打开串口（兼容所有浏览器）
    openPort: async function (config, dotNetHelper) {
        try {
            // 0. 先清理历史状态
            await this._safeCleanup();

            // 1. 检查浏览器支持
            if (!this.isSerialSupported()) {
                const browserName = this.getBrowserName();
                throw new Error(`浏览器 ${browserName} 不支持串口通信。请使用 Chrome 或 Edge 浏览器。`);
            }

            // 2. 检查是否已打开
            if (port && port.readable) {
                console.warn("串口已打开，跳过重复打开");
                return true;
            }

            // 3. 请求设备（浏览器兼容处理）
            let requestedPort;
            try {
                // Chrome/Edge 标准方式
                requestedPort = await navigator.serial.requestPort();
            } catch (error) {
                // Firefox 可能需要特殊处理
                if (this.getBrowserName() === "firefox") {
                    // Firefox 可能需要 about:config 中启用 dom.serial.enabled
                    throw new Error("Firefox 需要启用串口支持：在地址栏输入 about:config，搜索 dom.serial.enabled 并设为 true");
                }
                throw error;
            }

            // 4. 解析波特率与其它参数
            let baud = parseInt(config.baudRate, 10);
            if (!baud || baud <= 0 || Number.isNaN(baud)) {
                baud = parseInt(this.getPreferredScaleBaud(), 10) || DEFAULT_SCALE_BAUD;
            }

            const baseOpenOptions = {
                dataBits: parseInt(config.dataBits, 10) || 8,
                stopBits: parseInt(config.stopBits, 10) || 1,
                parity: (config.parity || 'none').toLowerCase(),
                bufferSize: 1024
            };

            if (baseOpenOptions.dataBits < 5 || baseOpenOptions.dataBits > 8) {
                baseOpenOptions.dataBits = 8;
            }
            if (baseOpenOptions.stopBits < 1 || baseOpenOptions.stopBits > 2) {
                baseOpenOptions.stopBits = 1;
            }

            const autoBaud = config.autoScaleBaud === true;
            const canAuto9600_19200 = autoBaud && (baud === 9600 || baud === 19200);

            let chosenBaud = baud;

            if (canAuto9600_19200) {
                const other = baud === 9600 ? 19200 : 9600;
                const tryOrder = [baud, other];
                let picked = null;

                for (const br of tryOrder) {
                    const openOptions = { ...baseOpenOptions, baudRate: br };
                    try {
                        await requestedPort.open(openOptions);
                    } catch (error) {
                        if (error.name === 'InvalidStateError') {
                            throw new Error("串口已被其他页面或应用程序占用。请先关闭其他页面的串口连接，再尝试打开。");
                        }
                        throw error;
                    }

                    try {
                        const dtr = config?.dtr !== false;
                        const rts = config?.rts !== false;
                        await requestedPort.setSignals({ dataTerminalReady: dtr, requestToSend: rts });
                    } catch (_) { }

                    const sample = await this._readProbeSample(requestedPort, 480, 512);
                    await this._notifyScaleBaudProbe(dotNetHelper, `[自动波特率] 试用 ${br}，采样 ${sample.length} 字节`);

                    try {
                        await requestedPort.close();
                    } catch (e) {
                        console.warn("探测关闭串口:", e);
                    }

                    if (!this._shouldRetryOtherBaud(sample)) {
                        picked = br;
                        await this._notifyScaleBaudProbe(dotNetHelper, `[自动波特率] 选用 ${br}`);
                        break;
                    }
                    await this._notifyScaleBaudProbe(dotNetHelper, `[自动波特率] ${br} 采样疑似乱码，尝试另一波特率`);
                }

                if (picked === null) {
                    picked = tryOrder[0];
                    await this._notifyScaleBaudProbe(dotNetHelper, `[自动波特率] 未明确判定，沿用首选 ${picked}`);
                }

                chosenBaud = picked;

                const finalOpen = { ...baseOpenOptions, baudRate: chosenBaud };
                try {
                    await requestedPort.open(finalOpen);
                } catch (error) {
                    if (error.name === 'InvalidStateError') {
                        throw new Error("串口已被其他页面或应用程序占用。请先关闭其他页面的串口连接，再尝试打开。");
                    }
                    throw error;
                }
            } else {
                const openOptions = { ...baseOpenOptions, baudRate: baud };
                try {
                    await requestedPort.open(openOptions);
                } catch (error) {
                    if (error.name === 'InvalidStateError') {
                        throw new Error("串口已被其他页面或应用程序占用。请先关闭其他页面的串口连接，再尝试打开。");
                    }
                    throw error;
                }
                chosenBaud = baud;
            }

            port = requestedPort;

            // 5.1 很多称重设备依赖 DTR/RTS，串口助手一般会默认拉高
            try {
                const dtr = config?.dtr !== false;
                const rts = config?.rts !== false;
                await port.setSignals({ dataTerminalReady: dtr, requestToSend: rts });
            } catch (_) {
                // 某些设备/浏览器不支持 setSignals，忽略
            }

            this.setPreferredScaleBaud(String(chosenBaud));

            const openOptionsLog = { ...baseOpenOptions, baudRate: chosenBaud };
            // 6. 开始读取循环（传入 config 以支持 readMode: ascii | hex）
            keepReading = true;
            readableStreamClosed = this.readLoop(dotNetHelper, config);

            console.log("串口打开成功，参数:", openOptionsLog, "称重数据: 自动识别(ASCII/Hex)");
            return true;

        } catch (error) {
            console.error("打开串口失败:", error);
            await this._safeCleanup();

            // 提供用户友好的错误信息
            const errorMessage = this.getErrorMessage(error);
            await dotNetHelper.invokeMethodAsync('OnError', errorMessage);

            return false;
        }
    },

    // 发送数据（兼容处理）
    writeData: async function (data) {
        if (!port || !port.writable) {
            throw new Error("串口未打开或不可写");
        }

        const writer = port.writable.getWriter();
        try {
            // 确保数据是 Uint8Array
            let buffer;
            if (typeof data === 'string') {
                const encoder = new TextEncoder();
                buffer = encoder.encode(data);
            } else if (data instanceof ArrayBuffer) {
                buffer = new Uint8Array(data);
            } else if (Array.isArray(data)) {
                buffer = new Uint8Array(data);
            } else {
                throw new Error("不支持的数据格式");
            }

            await writer.write(buffer);
            console.log("数据发送成功:", data);
        } finally {
            writer.releaseLock();
        }
    },

    // 发送十六进制数据
    writeHex: async function (hexString) {
        if (!port || !port.writable) {
            throw new Error("串口未打开或不可写");
        }

        // 移除空格和 0x 前缀
        const cleanHex = hexString.replace(/\s|0x/g, '').toLowerCase();

        // 验证十六进制格式
        if (!/^[0-9a-f]*$/.test(cleanHex) || cleanHex.length % 2 !== 0) {
            throw new Error("无效的十六进制格式");
        }

        const bytes = [];
        for (let i = 0; i < cleanHex.length; i += 2) {
            bytes.push(parseInt(cleanHex.substr(i, 2), 16));
        }

        await this.writeData(bytes);
    },

    // 关闭串口（兼容所有浏览器）
    closePort: async function () {
        try {
            await this._safeCleanup();
            console.log("串口已关闭");
            return true;
        } catch (error) {
            console.error("关闭串口时出错:", error);
            return false;
        }
    },

    // 获取已连接的串口列表（Chrome/Edge）
    getPorts: async function () {
        if (!this.isSerialSupported()) {
            return [];
        }

        try {
            const ports = await navigator.serial.getPorts();
            return ports.map((port, index) => ({
                id: index,
                name: port.getInfo().usbProductId ?
                    `USB设备 (VID: ${port.getInfo().usbVendorId}, PID: ${port.getInfo().usbProductId})` :
                    `串口 ${index + 1}`
            }));
        } catch (error) {
            console.error("获取串口列表失败:", error);
            return [];
        }
    },

    // 将字节数组转为十六进制字符串（无空格，小写）
    _bytesToHex: function (bytes) {
        return Array.from(bytes).map(b => ('0' + (b & 0xff).toString(16)).slice(-2)).join('');
    },

    // 字节按 ASCII 解码，非可打印字符替换为空格后 trim
    _bytesToAscii: function (bytes) {
        const decoder = new TextDecoder('ascii', { fatal: false });
        let s = decoder.decode(bytes);
        s = s.replace(/[\x00-\x1f\x7f-\xff]/g, ' ');
        return s.trim();
    },

    // 从字符串中提取「数字+小数点」称重值，单位会被去掉（如 "21.70 kg" → "21.70"）
    // 会先把数字与小数点之间的空格合并掉，如 "1 0 . 0 1" → "10.01"
    _extractWeight: function (str) {
        if (!str || typeof str !== 'string') return null;
        const normalized = str.replace(/([\d.](?:\s*[\d.])*)/g, function (run) {
            return run.replace(/\s/g, '');
        });
        const m = normalized.match(/[\d.]+/);
        return m ? m[0].trim() : null;
    },

    // 判断是否像十六进制字符串（仅 0-9A-Fa-f 与空格，且去空格后长度为偶）
    _isHexString: function (str) {
        if (!str || typeof str !== 'string') return false;
        const cleaned = str.replace(/\s/g, '');
        return /^[0-9A-Fa-f]+$/.test(cleaned) && cleaned.length % 2 === 0;
    },

    // 十六进制字符串转字节数组
    _hexStringToBytes: function (hexStr) {
        const cleaned = hexStr.replace(/\s/g, '');
        const bytes = [];
        for (let i = 0; i < cleaned.length; i += 2) {
            bytes.push(parseInt(cleaned.substr(i, 2), 16));
        }
        return new Uint8Array(bytes);
    },

    // 自动识别称重数据：先按 ASCII 提取重量；若像 hex 则按 hex 解码后再提取
    _normalizeWeight: function (byteArray) {
        const asciiStr = this._bytesToAscii(byteArray);
        const w = this._extractWeight(asciiStr);
        if (w) return w;
        if (this._isHexString(asciiStr)) {
            try {
                const decoded = this._hexStringToBytes(asciiStr);
                const decodedStr = this._bytesToAscii(decoded);
                const w2 = this._extractWeight(decodedStr);
                if (w2) return w2;
            } catch (_) { }
        }
        return asciiStr.length > 0 ? asciiStr : null;
    },

    // 读取循环：自动识别称重数据（ASCII 或 hex 字符串），统一提取「数字+小数点+单位」后发送
    readLoop: async function (dotNetHelper, config) {
        const FLUSH_DELAY_MS = 80;
        let flushTimer = null;
        let byteBuffer = [];

        const findCrLf = (arr) => {
            for (let i = 0; i < arr.length - 1; i++) {
                if (arr[i] === 0x0d && arr[i + 1] === 0x0a) return i;
            }
            return -1;
        };

        const sendSegment = async (bytes) => {
            if (bytes.length === 0) return;
            const result = this._normalizeWeight(new Uint8Array(bytes));
            if (result) await dotNetHelper.invokeMethodAsync('ReceiveData', result);
        };

        const flushByteBuffer = async () => {
            flushTimer = null;
            if (byteBuffer.length > 0) {
                await sendSegment(byteBuffer);
                byteBuffer = [];
            }
        };

        while (port.readable && keepReading) {
            reader = port.readable.getReader();
            try {
                while (true) {
                    const { value, done } = await reader.read();
                    if (done) break;
                    if (value && value.length > 0) {
                        for (let i = 0; i < value.length; i++) byteBuffer.push(value[i]);

                        let idx;
                        while ((idx = findCrLf(byteBuffer)) >= 0) {
                            if (flushTimer) { clearTimeout(flushTimer); flushTimer = null; }
                            const lineBytes = byteBuffer.splice(0, idx);
                            byteBuffer.splice(0, 2);
                            await sendSegment(lineBytes);
                        }

                        if (byteBuffer.length > 0) {
                            if (flushTimer) clearTimeout(flushTimer);
                            flushTimer = setTimeout(() => flushByteBuffer(), FLUSH_DELAY_MS);
                        }
                    }
                }
                if (flushTimer) { clearTimeout(flushTimer); await flushByteBuffer(); }
            } catch (error) {
                console.error("读取出错:", error);
                if (keepReading) {
                    const n = error?.name ? `${error.name}: ` : "";
                    await dotNetHelper.invokeMethodAsync('OnError', `读取错误: ${n}${error.message}`);
                }
            } finally {
                if (flushTimer) clearTimeout(flushTimer);
                reader.releaseLock();
            }
        }
    },

    // 错误信息处理
    getErrorMessage: function (error) {
        const browserName = this.getBrowserName();

        switch (error.name) {
            case 'NotFoundError':
                return "未找到串口设备。请确保设备已连接。";
            case 'SecurityError':
                return "安全错误：请确保页面使用 HTTPS 协议（localhost 除外）。";
            case 'NetworkError':
                return `网络错误：串口通信中断（${error.message || "设备断开/驱动复位"}）`;
            case 'InvalidStateError':
                return "串口状态无效：可能已被其他程序占用。";
            default:
                if (browserName === 'firefox') {
                    return `Firefox 错误：${error.message}。需要在 about:config 中启用 dom.serial.enabled。`;
                }
                return `错误：${error.message}`;
        }
    },

    // 获取串口信息
    getPortInfo: function () {
        if (!port) return null;

        try {
            const info = port.getInfo();
            return {
                vendorId: info.usbVendorId || '未知',
                productId: info.usbProductId || '未知',
                baudRate: port.baudRate || '未知'
            };
        } catch (error) {
            return null;
        }
    }
};