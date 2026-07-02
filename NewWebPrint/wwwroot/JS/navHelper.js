// 导航、下载触发（供 Blazor IJSRuntime 调用）
window.navHelper = {
    back: function () {
        history.back();
    }
};

window.triggerClientDownload = function () {
    const a = document.getElementById("downloadClient");
    if (a) a.click();
};

window.triggerVcRedistDownload = function () {
    const a = document.getElementById("downloadVcRedist");
    if (a) a.click();
};

window.triggerApkDownload = function () {
    var el = document.getElementById("downloadApk");
    if (el) el.click();
};
