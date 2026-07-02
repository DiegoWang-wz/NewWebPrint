// wwwroot/js/localAgent.js

const STORAGE_KEY = "printAgentBaseUrl";
const DEFAULT_BASE_URL = "http://localhost:5005";

/** 是否为「本机」地址：未填 IP 时用此地址，由浏览器直连，走的是当前电脑的 localhost */
function isLocalhostBaseUrl(url) {
    if (!url || typeof url !== "string") return true;
    const u = url.trim().toLowerCase().replace(/\/+$/, "");
    return u === "http://localhost:5005" || u === "http://127.0.0.1:5005";
}

function getStoredBaseUrl() {
    try {
        const s = localStorage.getItem(STORAGE_KEY);
        if (!s || typeof s !== "string") return DEFAULT_BASE_URL;
        // C# Blazored.LocalStorage 会按 JSON 存储，需解析
        var parsed = s.trim();
        if (parsed.startsWith('"')) {
            try { parsed = JSON.parse(s); } catch (_) {}
        }
        if (typeof parsed === "string" && parsed.trim()) return parsed.trim();
    } catch (_) {}
    return DEFAULT_BASE_URL;
}

window.localAgent = {
    get baseUrl() {
        return getStoredBaseUrl();
    },

    setBaseUrl(url) {
        const v = (url && typeof url === "string") ? url.trim() : "";
        if (v) localStorage.setItem(STORAGE_KEY, v);
        else localStorage.removeItem(STORAGE_KEY);
    },

    getBaseUrl() {
        return getStoredBaseUrl();
    },

    // 获取打印机列表：未填 IP（localhost）时由浏览器直连本机 5005；否则经服务端代理转发
    async getPrinters() {
        const url = this.baseUrl.replace(/\/+$/, "") + "/printers";
        if (isLocalhostBaseUrl(this.baseUrl)) {
            const resp = await fetch(url, { method: "GET", mode: "cors" });
            if (!resp.ok) {
                let text = "";
                try { text = await resp.text(); } catch { }
                throw new Error("GET /printers 失败: HTTP " + resp.status + " " + resp.statusText + " " + text);
            }
            return await resp.json();
        }
        const proxyUrl = "/api/printagent/printers?baseUrl=" + encodeURIComponent(this.baseUrl);
        const resp = await fetch(proxyUrl, { method: "GET" });
        if (!resp.ok) {
            let text = "";
            try { text = await resp.text(); } catch { }
            throw new Error("GET /printers 失败: HTTP " + resp.status + " " + resp.statusText + " " + text);
        }
        return await resp.json();
    },

    // 发送打印布局：未填 IP（localhost）时由浏览器直连本机 5005；否则经服务端代理转发
    async printLayout(layout) {
        const url = this.baseUrl.replace(/\/+$/, "") + "/print/layout";
        if (isLocalhostBaseUrl(this.baseUrl)) {
            const resp = await fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(layout)
            });
            if (!resp.ok) {
                let text = "";
                try { text = await resp.text(); } catch { }
                throw new Error("POST /print/layout 失败: HTTP " + resp.status + " " + resp.statusText + " " + text);
            }
            return true;
        }
        const proxyUrl = "/api/printagent/print/layout?baseUrl=" + encodeURIComponent(this.baseUrl);
        const resp = await fetch(proxyUrl, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(layout)
        });
        if (!resp.ok) {
            let text = "";
            try { text = await resp.text(); } catch { }
            throw new Error("POST /print/layout 失败: HTTP " + resp.status + " " + resp.statusText + " " + text);
        }
        return true;
    }
};
