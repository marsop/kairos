window.kairosFile = window.kairosFile || {
    downloadText: function (request) {
        if (!request || !request.fileName) {
            return;
        }

        var blob = new Blob(
            [request.content || ""],
            { type: request.contentType || "text/plain;charset=utf-8" });

        var url = URL.createObjectURL(blob);
        var link = document.createElement("a");
        link.href = url;
        link.download = request.fileName;
        link.style.display = "none";

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);

        setTimeout(function () {
            URL.revokeObjectURL(url);
        }, 0);
    }
};
