window.notificationInterop = {
    requestPermission: async function () {
        if (!('Notification' in window)) {
            return 'denied';
        }
        return await Notification.requestPermission();
    },

    getPermissionState: function () {
        if (!('Notification' in window)) {
            return 'denied';
        }
        return Notification.permission;
    },

    showNotification: function (title, body) {
        if ('Notification' in window && Notification.permission === 'granted') {
            const notification = new Notification(title, {
                body: body,
                icon: 'appicon.svg'
            });
            notification.onclick = function() {
                window.focus();
                this.close();
            };
        }
    }
};
