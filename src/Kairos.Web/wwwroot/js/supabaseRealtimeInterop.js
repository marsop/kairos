window.kairosSupabaseRealtime = (() => {
    const heartbeatIntervalMs = 25000;
    const notifyDebounceMs = 150;

    const state = {
        socket: null,
        dotNetRef: null,
        config: null,
        heartbeatTimer: null,
        reconnectTimer: null,
        reconnectAttempts: 0,
        nextRef: 1,
        joinRef: null,
        topic: null,
        pendingTables: new Map()
    };

    function initialize(dotNetRef) {
        state.dotNetRef = dotNetRef;
    }

    function connect(config) {
        if (!config || !config.url || !config.anonKey || !config.accessToken || !config.userId) {
            disconnect();
            return;
        }

        const normalizedConfig = {
            url: config.url,
            anonKey: config.anonKey,
            accessToken: config.accessToken,
            userId: config.userId
        };

        const shouldReconnect =
            !state.config ||
            state.config.url !== normalizedConfig.url ||
            state.config.anonKey !== normalizedConfig.anonKey ||
            state.config.accessToken !== normalizedConfig.accessToken ||
            state.config.userId !== normalizedConfig.userId ||
            !state.socket ||
            state.socket.readyState === WebSocket.CLOSING ||
            state.socket.readyState === WebSocket.CLOSED;

        state.config = normalizedConfig;

        if (!shouldReconnect) {
            return;
        }

        openSocket();
    }

    function disconnect() {
        clearReconnect();
        clearHeartbeat();
        clearPendingTableNotifications();

        state.config = null;
        state.topic = null;
        state.joinRef = null;
        state.reconnectAttempts = 0;

        if (state.socket) {
            const socket = state.socket;
            state.socket = null;

            try {
                if (socket.readyState === WebSocket.OPEN) {
                    send("phx_leave", {}, state.topic ?? "phoenix");
                }
                socket.close();
            } catch {
                // Ignore disconnect errors.
            }
        }
    }

    function openSocket() {
        clearReconnect();
        clearHeartbeat();
        clearPendingTableNotifications();

        if (state.socket) {
            try {
                state.socket.close();
            } catch {
                // Ignore reconnect teardown issues.
            }
        }

        state.joinRef = `${state.nextRef++}`;
        state.topic = `realtime:kairos-sync-${state.config.userId}`;

        const wsUrl = buildRealtimeUrl(state.config.url, state.config.anonKey);
        const socket = new WebSocket(wsUrl);
        state.socket = socket;

        socket.onopen = () => {
            state.reconnectAttempts = 0;
            startHeartbeat();
            sendJoin();
        };

        socket.onmessage = (event) => {
            handleMessage(event.data);
        };

        socket.onclose = () => {
            clearHeartbeat();
            if (state.socket === socket) {
                state.socket = null;
            }

            if (state.config) {
                scheduleReconnect();
            }
        };

        socket.onerror = () => {
            if (state.socket === socket) {
                try {
                    socket.close();
                } catch {
                    // Ignore socket close errors.
                }
            }
        };
    }

    function buildRealtimeUrl(url, anonKey) {
        const wsBase = url.replace(/^https/i, "wss").replace(/^http/i, "ws").replace(/\/$/, "");
        return `${wsBase}/realtime/v1/websocket?apikey=${encodeURIComponent(anonKey)}&vsn=1.0.0`;
    }

    function sendJoin() {
        send("phx_join", {
            config: {
                broadcast: {
                    ack: false,
                    self: false
                },
                presence: {
                    enabled: false,
                    key: ""
                },
                postgres_changes: [
                    {
                        event: "*",
                        schema: "public",
                        table: "activities",
                        filter: `user_id=eq.${state.config.userId}`
                    },
                    {
                        event: "*",
                        schema: "public",
                        table: "time_accounts",
                        filter: `user_id=eq.${state.config.userId}`
                    },
                    {
                        event: "*",
                        schema: "public",
                        table: "user_settings",
                        filter: `user_id=eq.${state.config.userId}`
                    }
                ],
                private: false
            },
            access_token: state.config.accessToken
        }, state.topic, state.joinRef);
    }

    function send(event, payload, topic, joinRefOverride) {
        if (!state.socket || state.socket.readyState !== WebSocket.OPEN) {
            return;
        }

        const ref = `${state.nextRef++}`;
        state.socket.send(JSON.stringify({
            topic,
            event,
            payload,
            ref,
            join_ref: joinRefOverride ?? state.joinRef
        }));
    }

    function handleMessage(rawMessage) {
        let message;

        try {
            message = JSON.parse(rawMessage);
        } catch {
            return;
        }

        if (!message || message.event !== "postgres_changes") {
            return;
        }

        const change = message.payload?.data;
        const table = change?.table;
        if (!table) {
            return;
        }

        const changeUserId = change.record?.user_id ?? change.old_record?.user_id ?? null;
        if (changeUserId && changeUserId !== state.config?.userId) {
            return;
        }

        debounceTableNotification(table);
    }

    function debounceTableNotification(table) {
        const existing = state.pendingTables.get(table);
        if (existing) {
            clearTimeout(existing);
        }

        const timer = setTimeout(() => {
            state.pendingTables.delete(table);
            notifyTableChanged(table);
        }, notifyDebounceMs);

        state.pendingTables.set(table, timer);
    }

    function notifyTableChanged(table) {
        if (!state.dotNetRef) {
            return;
        }

        state.dotNetRef.invokeMethodAsync("NotifyTableChanged", table)
            .catch(() => {
                // Ignore interop failures during teardown/navigation.
            });
    }

    function startHeartbeat() {
        clearHeartbeat();
        state.heartbeatTimer = setInterval(() => {
            send("heartbeat", {}, "phoenix", null);
        }, heartbeatIntervalMs);
    }

    function clearHeartbeat() {
        if (state.heartbeatTimer) {
            clearInterval(state.heartbeatTimer);
            state.heartbeatTimer = null;
        }
    }

    function scheduleReconnect() {
        clearReconnect();
        const delayMs = Math.min(30000, 1000 * Math.max(1, Math.pow(2, state.reconnectAttempts)));
        state.reconnectAttempts += 1;
        state.reconnectTimer = setTimeout(() => {
            state.reconnectTimer = null;
            if (state.config) {
                openSocket();
            }
        }, delayMs);
    }

    function clearReconnect() {
        if (state.reconnectTimer) {
            clearTimeout(state.reconnectTimer);
            state.reconnectTimer = null;
        }
    }

    function clearPendingTableNotifications() {
        for (const timer of state.pendingTables.values()) {
            clearTimeout(timer);
        }

        state.pendingTables.clear();
    }

    return {
        initialize,
        connect,
        disconnect
    };
})();
