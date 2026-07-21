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
    },
    animateCalendarZoom: function (element, startZoom, targetZoom, startScrollTop, targetScrollTop, durationMs, sequential, dotNetHelper) {
        if (!element) return;

        if (element._currentAnimation) {
            cancelAnimationFrame(element._currentAnimation);
        }

        var startTime = null;

        function easeInOutQuad(t) {
            return t < 0.5 ? 2 * t * t : -1 + (4 - 2 * t) * t;
        }

        function step(timestamp) {
            if (!startTime) startTime = timestamp;
            var progress = timestamp - startTime;
            var t = Math.min(progress / durationMs, 1);

            if (sequential) {
                // Sequential: Scroll first, then Zoom
                var scrollProgress = Math.min(progress / (durationMs * 0.5), 1);
                var zoomProgress = Math.max(0, Math.min((progress - durationMs * 0.5) / (durationMs * 0.5), 1));

                var easedScroll = easeInOutQuad(scrollProgress);
                var easedZoom = easeInOutQuad(zoomProgress);

                var currentScroll = startScrollTop + (targetScrollTop - startScrollTop) * easedScroll;
                var currentZoom = startZoom + (targetZoom - startZoom) * easedZoom;

                element.scrollTop = currentScroll;
                element.style.setProperty('--pixels-per-hour', currentZoom);
            } else {
                // Concurrent: Scroll and Zoom together
                var easedT = easeInOutQuad(t);

                var currentScroll = startScrollTop + (targetScrollTop - startScrollTop) * easedT;
                var currentZoom = startZoom + (targetZoom - startZoom) * easedT;

                element.style.setProperty('--pixels-per-hour', currentZoom);
                element.scrollTop = currentScroll;
            }

            if (progress < durationMs) {
                element._currentAnimation = requestAnimationFrame(step);
            } else {
                element._currentAnimation = null;
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnAnimationComplete', targetZoom);
                }
            }
        }

        element._currentAnimation = requestAnimationFrame(step);
    }
};
