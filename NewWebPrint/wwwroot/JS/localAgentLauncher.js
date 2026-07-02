// 通过 localagent:// 唤起本机 LocalAgent，并轮询 /health 等待就绪
// 总等待时长由 C# LocalAgentPrinterBootstrap.LocalAgentOpenWaitMs 传入 openAndWait(timeoutMs)
window.localAgentLauncher = {
    open() {
        window.location.href = "localagent://open";
    },

    async _pingHealth(healthUrl, timeoutMs = 3000) {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), timeoutMs);
        try {
            const resp = await fetch(healthUrl, {
                method: "GET",
                mode: "cors",
                signal: controller.signal
            });
            return resp.ok;
        } catch {
            return false;
        } finally {
            clearTimeout(timer);
        }
    },

    async waitReady(timeoutMs = 8000, intervalMs = 500) {
        const base = window.localAgent?.getBaseUrl?.() ?? "http://localhost:5005";
        const healthUrl = base.replace(/\/+$/, "") + "/health";
        const deadline = Date.now() + timeoutMs;

        while (Date.now() < deadline) {
            if (await this._pingHealth(healthUrl)) return true;
            await new Promise(r => setTimeout(r, intervalMs));
        }
        return false;
    },

    async openAndWait(timeoutMs = 8000) {
        this.open();
        return await this.waitReady(timeoutMs);
    }
};
