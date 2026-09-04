// Excel / 文件下载辅助
window.blazorDownloadExcel = function (fileName, base64) {
    try {
        const link = document.createElement("a");
        link.download = fileName || "export.xlsx";
        link.href = "data:application/vnd.openxmlformats-officedocument.spreadsheetml.sheet;base64," + base64;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (e) {
        console.error("blazorDownloadExcel failed", e);
        throw e;
    }
};
