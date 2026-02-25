window.downloadFile = function (filename, base64Content) {
    const linkSource = `data:text/csv;base64,${base64Content}`;
    const downloadLink = document.createElement("a");
    downloadLink.href = linkSource;
    downloadLink.download = filename;
    downloadLink.click();
};

window.scrollMqttTerminalToBottom = function () {
    const el = document.getElementById("mqttTerminal");
    if (el) {
        el.scrollTop = el.scrollHeight;
    }
};

