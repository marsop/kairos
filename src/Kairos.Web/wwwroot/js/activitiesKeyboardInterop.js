window.activitiesKeyboardInterop = {
    _dotNetRef: null,
    _keydownHandler: function (e) {
        if (e.repeat) return;

        // Ignore if focus is on input, textarea, select
        const tagName = document.activeElement ? document.activeElement.tagName.toLowerCase() : '';
        if (tagName === 'input' || tagName === 'textarea' || tagName === 'select') {
            return;
        }

        if (e.code && e.code.startsWith('Numpad')) {
            const num = parseInt(e.code.replace('Numpad', ''), 10);
            if (!isNaN(num) && num >= 1 && num <= 8) {
                if (window.activitiesKeyboardInterop._dotNetRef) {
                    window.activitiesKeyboardInterop._dotNetRef.invokeMethodAsync('OnNumpadKeyPress', num);
                }
            }
        }
    },
    register: function (dotNetRef) {
        window.activitiesKeyboardInterop._dotNetRef = dotNetRef;
        window.addEventListener('keydown', window.activitiesKeyboardInterop._keydownHandler);
    },
    unregister: function () {
        window.removeEventListener('keydown', window.activitiesKeyboardInterop._keydownHandler);
        window.activitiesKeyboardInterop._dotNetRef = null;
    }
};