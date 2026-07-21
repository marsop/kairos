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
    getCurrentZoom: function (element) {
        return element && typeof element._currentZoom === 'number' ? element._currentZoom : 0;
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
    animateCalendarZoom: function (element, startZoom, targetZoom, startScrollTop, durationMs, anchorHours, anchorOffsetY, prePanRatio, dotNetHelper) {
        if (!element) return;

        if (element._currentAnimation) {
            cancelAnimationFrame(element._currentAnimation);
        }

        var startTime = null;

        function lerp(start, end, t) {
            return start + (end - start) * t;
        }

        function clamp(value, min, max) {
            return Math.max(min, Math.min(max, value));
        }

        function easeOutCubic(t) {
            return 1 - Math.pow(1 - t, 3);
        }

        function easeInOutCubic(t) {
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.pow(-2 * t + 2, 3) / 2;
        }

        function clampScrollTop(scrollTop, zoom) {
            var contentHeight = zoom * 24;
            var maxScrollTop = Math.max(0, contentHeight - element.clientHeight);
            return clamp(scrollTop, 0, maxScrollTop);
        }

        function applyZoomFrame(zoom) {
            var dayHeight = zoom * 24;

            var yAxis = element.querySelector('.calendar-y-axis');
            var grid = element.querySelector('.calendar-grid');
            var events = element.querySelector('.calendar-events');

            if (yAxis) {
                yAxis.style.height = dayHeight + 'px';
            }

            if (grid) {
                grid.style.height = dayHeight + 'px';
            }

            if (events) {
                events.style.height = dayHeight + 'px';
            }

            var hourMarkers = element.querySelectorAll('.calendar-hour-marker');
            for (var i = 0; i < hourMarkers.length; i++) {
                var markerTop = i * zoom;
                hourMarkers[i].style.top = markerTop + 'px';
                hourMarkers[i].style.height = zoom + 'px';
            }

            var gridLines = element.querySelectorAll('.calendar-grid-line');
            for (var j = 0; j < gridLines.length; j++) {
                gridLines[j].style.top = (j * zoom) + 'px';
            }

            var blocks = element.querySelectorAll('.calendar-event-block');
            for (var k = 0; k < blocks.length; k++) {
                var block = blocks[k];
                var startSeconds = parseFloat(block.dataset.startSeconds || '0');
                var endSeconds = parseFloat(block.dataset.endSeconds || '0');

                var topPx = (startSeconds / 3600.0) * zoom;
                var durationHours = Math.max(0, (endSeconds - startSeconds) / 3600.0);
                var heightPx = Math.max(15, durationHours * zoom);

                block.style.top = topPx + 'px';
                block.style.height = heightPx + 'px';
            }

            element._currentZoom = zoom;
        }

        prePanRatio = clamp(typeof prePanRatio === 'number' ? prePanRatio : 0, 0, 0.9);

        function step(timestamp) {
            if (!startTime) startTime = timestamp;
            var progress = timestamp - startTime;
            var t = Math.min(progress / durationMs, 1);
            var currentZoom = startZoom;
            var currentScrollTop = startScrollTop;

            if (prePanRatio > 0 && t < prePanRatio) {
                var panT = t / prePanRatio;
                var easedPan = easeOutCubic(panT);
                var centeredAtStartZoom = anchorHours * startZoom - anchorOffsetY;

                currentZoom = startZoom;
                currentScrollTop = lerp(startScrollTop, centeredAtStartZoom, easedPan);
            } else {
                var zoomT = prePanRatio >= 1 ? 1 : (t - prePanRatio) / (1 - prePanRatio);
                zoomT = clamp(zoomT, 0, 1);
                var easedZoom = easeInOutCubic(zoomT);

                currentZoom = lerp(startZoom, targetZoom, easedZoom);
                currentScrollTop = anchorHours * currentZoom - anchorOffsetY;
            }

            applyZoomFrame(currentZoom);
            element.scrollTop = clampScrollTop(currentScrollTop, currentZoom);

            if (progress < durationMs) {
                element._currentAnimation = requestAnimationFrame(step);
            } else {
                element._currentAnimation = null;
                applyZoomFrame(targetZoom);
                element.scrollTop = clampScrollTop(anchorHours * targetZoom - anchorOffsetY, targetZoom);
                if (dotNetHelper) {
                    dotNetHelper.invokeMethodAsync('OnAnimationComplete', targetZoom);
                }
            }
        }

        element._currentAnimation = requestAnimationFrame(step);
    }
};
