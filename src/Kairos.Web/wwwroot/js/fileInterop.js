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

window.kairosScroll = window.kairosScroll || {
    setTop: function (element, scrollTop) {
        if (element) {
            element.scrollTop = scrollTop;
        }
    },
    getTop: function (element) {
        return element ? element.scrollTop : 0;
    },
    getClientHeight: function (element) {
        return element ? element.clientHeight : 0;
    },
    initZoom: function (element, dotNetHelper) {
        if (!element) return;

        // Prevent multiple listeners
        if (element._hasZoomListener) return;
        element._hasZoomListener = true;

        element.addEventListener('wheel', function (e) {
            if (e.ctrlKey || e.metaKey) {
                e.preventDefault();

                var rect = element.getBoundingClientRect();
                var offsetY = e.clientY - rect.top;

                dotNetHelper.invokeMethodAsync('OnWheelZoom', e.deltaY, offsetY, element.scrollTop, element.clientHeight);
            }
        }, { passive: false });
    }
};
