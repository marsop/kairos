// Supabase JavaScript interop for Budgetr
window.supabaseInterop = {
    // Internal state
    _client: null,
    _isInitialized: false,

    // Initialize the Supabase client
    initialize: async function (supabaseUrl, anonKey) {
        if (this._isInitialized && this._client) {
            return true;
        }

        try {
            // Dynamically load the Supabase JS client from CDN if not already loaded
            if (typeof supabase === 'undefined' || !supabase.createClient) {
                await this._loadScript('https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2/dist/umd/supabase.min.js');
            }

            this._client = supabase.createClient(supabaseUrl, anonKey);
            this._isInitialized = true;
            return true;
        } catch (error) {
            console.error('Failed to initialize Supabase:', error);
            return false;
        }
    },

    // Dynamically load a script
    _loadScript: function (src) {
        return new Promise((resolve, reject) => {
            // Check if already loaded
            if (document.querySelector(`script[src="${src}"]`)) {
                resolve();
                return;
            }

            const script = document.createElement('script');
            script.src = src;
            script.onload = resolve;
            script.onerror = () => reject(new Error(`Failed to load script: ${src}`));
            document.head.appendChild(script);
        });
    },

    // Sign up with email and password
    signUp: async function (email, password) {
        if (!this._client) throw new Error('Supabase not initialized');

        const { data, error } = await this._client.auth.signUp({
            email: email,
            password: password
        });

        if (error) throw new Error(error.message);
        return true;
    },

    // Sign in with email and password
    signIn: async function (email, password) {
        if (!this._client) throw new Error('Supabase not initialized');

        const { data, error } = await this._client.auth.signInWithPassword({
            email: email,
            password: password
        });

        if (error) throw new Error(error.message);
        return true;
    },

    // Sign out
    signOut: async function () {
        if (!this._client) return;

        const { error } = await this._client.auth.signOut();
        if (error) {
            console.warn('Supabase sign out error:', error.message);
        }
    },

    // Check if user is signed in
    isSignedIn: async function () {
        if (!this._client) return false;

        try {
            const { data: { session } } = await this._client.auth.getSession();
            return !!session;
        } catch {
            return false;
        }
    },

    // Get current user's email
    getUserEmail: async function () {
        if (!this._client) return null;

        try {
            const { data: { user } } = await this._client.auth.getUser();
            return user ? user.email : null;
        } catch {
            return null;
        }
    },

    // Upload (upsert) data to the user_data table
    uploadData: async function (jsonData) {
        if (!this._client) throw new Error('Supabase not initialized');

        const { data: { user } } = await this._client.auth.getUser();
        if (!user) throw new Error('Not signed in');

        const now = new Date().toISOString();

        // Try to upsert (insert or update on conflict)
        const { data, error } = await this._client.from('user_data')
            .upsert({
                user_id: user.id,
                data: JSON.parse(jsonData),
                updated_at: now
            }, {
                onConflict: 'user_id'
            })
            .select('updated_at')
            .single();

        if (error) throw new Error(error.message);

        return data ? data.updated_at : now;
    },

    // Download data from the user_data table
    downloadData: async function () {
        if (!this._client) throw new Error('Supabase not initialized');

        const { data: { user } } = await this._client.auth.getUser();
        if (!user) throw new Error('Not signed in');

        const { data, error } = await this._client.from('user_data')
            .select('data, updated_at')
            .eq('user_id', user.id)
            .single();

        if (error) {
            if (error.code === 'PGRST116') {
                // No rows found - no backup exists
                return null;
            }
            throw new Error(error.message);
        }

        return data ? JSON.stringify(data.data) : null;
    },

    // Get the last backup time
    getLastBackupTime: async function () {
        if (!this._client) return null;

        try {
            const { data: { user } } = await this._client.auth.getUser();
            if (!user) return null;

            const { data, error } = await this._client.from('user_data')
                .select('updated_at')
                .eq('user_id', user.id)
                .single();

            if (error || !data) return null;
            return data.updated_at;
        } catch {
            return null;
        }
    }
};
