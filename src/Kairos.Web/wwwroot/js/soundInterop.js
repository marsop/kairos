window.kairosSound = {
    _context: null,

    _getContext: function () {
        if (!this._context) {
            this._context = new (window.AudioContext || window.webkitAudioContext)();
        }
        // Resume if suspended (browsers require a user gesture before audio can play)
        if (this._context.state === 'suspended') {
            this._context.resume();
        }
        return this._context;
    },

    // Short ascending two-tone chirp: played when an activity starts
    playActivityStart: function () {
        try {
            const ctx = this._getContext();
            const now = ctx.currentTime;
            this._playTone(ctx, 523, now,        0.12, 0.10); // C5
            this._playTone(ctx, 784, now + 0.10, 0.12, 0.14); // G5
        } catch (e) {
            // Ignore audio errors (unsupported browser, blocked context, etc.)
        }
    },

    // Soft descending two-tone: played when an activity stops
    playActivityStop: function () {
        try {
            const ctx = this._getContext();
            const now = ctx.currentTime;
            this._playTone(ctx, 659, now,        0.10, 0.10); // E5
            this._playTone(ctx, 440, now + 0.10, 0.10, 0.14); // A4
        } catch (e) {
            // Ignore audio errors
        }
    },

    _playTone: function (ctx, frequency, startTime, volume, duration) {
        const oscillator = ctx.createOscillator();
        const gainNode = ctx.createGain();

        oscillator.connect(gainNode);
        gainNode.connect(ctx.destination);

        oscillator.type = 'sine';
        oscillator.frequency.setValueAtTime(frequency, startTime);

        gainNode.gain.setValueAtTime(0, startTime);
        gainNode.gain.linearRampToValueAtTime(volume, startTime + 0.01);
        gainNode.gain.exponentialRampToValueAtTime(0.001, startTime + duration);

        oscillator.start(startTime);
        oscillator.stop(startTime + duration);
    }
};
