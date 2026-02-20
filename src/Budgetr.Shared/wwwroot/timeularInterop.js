// Timeular device JavaScript interop for Budgetr
window.timeularInterop = {
    ORIENTATION_SERVICE_UUID: "c7e70010-c847-11e6-8175-8c89a55d403c",
    ORIENTATION_CHARACTERISTIC_UUID: "c7e70012-c847-11e6-8175-8c89a55d403c",

    _device: null,
    _server: null,
    _orientationCharacteristic: null,
    _orientationHandler: null,
    _dotNetRef: null,
    STORAGE_KEY: "budgetr_timeular_state",

    registerListener: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
    },

    unregisterListener: function () {
        this._dotNetRef = null;
    },

    requestAndConnect: async function () {
        if (!navigator.bluetooth) {
            return {
                success: false,
                message: "Web Bluetooth is not supported in this browser."
            };
        }

        try {
            const device = await navigator.bluetooth.requestDevice({
                filters: [
                    { namePrefix: "Timeular" },
                    { namePrefix: "ZEI" }
                ],
                optionalServices: [
                    this.ORIENTATION_SERVICE_UUID,
                    "battery_service",
                    "device_information"
                ]
            });

            this._device = device;

            if (device.gatt && !device.gatt.connected) {
                this._server = await device.gatt.connect();
            } else {
                this._server = device.gatt;
            }

            const deviceName = device.name || "Timeular Device";
            const deviceId = device.id || null;

            await this._subscribeToOrientationChanges();
            this._attachDisconnectHandler();

            this.saveState(deviceName, deviceId);

            return {
                success: true,
                deviceName: deviceName,
                deviceId: deviceId
            };
        } catch (error) {
            if (error && error.name === "NotFoundError") {
                return {
                    success: false,
                    message: "Device selection was cancelled."
                };
            }

            return {
                success: false,
                message: error?.message || "Unable to connect to Timeular device."
            };
        }
    },

    reconnectSavedDevice: async function () {
        if (!navigator.bluetooth || !navigator.bluetooth.getDevices) {
            return {
                attempted: false,
                success: false,
                message: "Auto-reconnect is not supported in this browser."
            };
        }

        const saved = this.getSavedState();
        if (!saved || !saved.deviceId) {
            return {
                attempted: false,
                success: false,
                message: "No previously connected Timeular device found."
            };
        }

        try {
            const devices = await navigator.bluetooth.getDevices();
            const matched = devices.find(d => d && d.id === saved.deviceId);

            if (!matched) {
                return {
                    attempted: true,
                    success: false,
                    message: "Previously connected Timeular device is not available."
                };
            }

            this._device = matched;
            if (matched.gatt && !matched.gatt.connected) {
                this._server = await matched.gatt.connect();
            } else {
                this._server = matched.gatt;
            }

            const deviceName = matched.name || saved.deviceName || "Timeular Device";
            const deviceId = matched.id || saved.deviceId || null;

            await this._subscribeToOrientationChanges();
            this._attachDisconnectHandler();
            this.saveState(deviceName, deviceId);

            return {
                attempted: true,
                success: true,
                deviceName: deviceName,
                deviceId: deviceId
            };
        } catch (error) {
            return {
                attempted: true,
                success: false,
                message: error?.message || "Unable to reconnect to saved Timeular device."
            };
        }
    },

    _subscribeToOrientationChanges: async function () {
        if (!this._server) {
            throw new Error("GATT server is not connected.");
        }

        const service = await this._server.getPrimaryService(this.ORIENTATION_SERVICE_UUID);
        const characteristic = await service.getCharacteristic(this.ORIENTATION_CHARACTERISTIC_UUID);

        this._orientationCharacteristic = characteristic;
        await characteristic.startNotifications();

        this._orientationHandler = (event) => {
            const value = event?.target?.value;
            if (!value) {
                return;
            }

            const bytes = Array.from(new Uint8Array(value.buffer));
            const hex = bytes.map(b => b.toString(16).padStart(2, "0")).join(" ");
            const face = bytes.length > 0 ? bytes[0] : null;

            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync("OnTimeularChange", {
                    eventType: "orientation",
                    face: face,
                    rawHex: hex,
                    timestampUtc: new Date().toISOString()
                });
            }
        };

        characteristic.addEventListener("characteristicvaluechanged", this._orientationHandler);
    },

    _attachDisconnectHandler: function () {
        if (!this._device) {
            return;
        }

        this._device.addEventListener("gattserverdisconnected", () => {
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync("OnTimeularChange", {
                    eventType: "disconnected",
                    timestampUtc: new Date().toISOString()
                });
            }
        });
    },

    disconnect: function () {
        if (this._orientationCharacteristic && this._orientationHandler) {
            this._orientationCharacteristic.removeEventListener("characteristicvaluechanged", this._orientationHandler);
        }

        if (this._device && this._device.gatt && this._device.gatt.connected) {
            this._device.gatt.disconnect();
        }

        this._orientationCharacteristic = null;
        this._orientationHandler = null;
        this._server = null;
    },

    saveState: function (deviceName, deviceId) {
        const payload = {
            deviceName: deviceName || null,
            deviceId: deviceId || null,
            connectedAtUtc: new Date().toISOString()
        };

        localStorage.setItem(this.STORAGE_KEY, JSON.stringify(payload));
    },

    getSavedState: function () {
        const raw = localStorage.getItem(this.STORAGE_KEY);
        if (!raw) {
            return null;
        }

        try {
            return JSON.parse(raw);
        } catch {
            return null;
        }
    }
};
