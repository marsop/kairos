window.pwaInterop = {
    deferredPrompt: null,
    dotNetRef: null,

    init: function (dotNetRef) {
        this.dotNetRef = dotNetRef;

        window.addEventListener('beforeinstallprompt', (e) => {
            // Prevent Chrome 67 and earlier from automatically showing the prompt
            e.preventDefault();
            // Stash the event so it can be triggered later.
            this.deferredPrompt = e;
            // Notify .NET that installation is available
            this.dotNetRef.invokeMethodAsync('OnInstallable', true);
        });

        window.addEventListener('appinstalled', () => {
            this.deferredPrompt = null;
            this.dotNetRef.invokeMethodAsync('OnInstallable', false);
        });

        window.addEventListener('online', () => {
            this.dotNetRef.invokeMethodAsync('OnConnectionChanged', true);
        });

        window.addEventListener('offline', () => {
            this.dotNetRef.invokeMethodAsync('OnConnectionChanged', false);
        });
    },

    triggerInstall: async function () {
        if (this.deferredPrompt) {
            this.deferredPrompt.prompt();
            const { outcome } = await this.deferredPrompt.userChoice;
            this.deferredPrompt = null;
            // Notify .NET about the result (optional, or just assume it's handled by appinstalled)
        }
    },

    getOnlineStatus: function () {
        return navigator.onLine;
    }
};
