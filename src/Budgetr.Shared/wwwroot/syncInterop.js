// Sync functionality JavaScript interop
window.syncInterop = {
    // Download a file with the given content
    downloadFile: function (filename, content) {
        const blob = new Blob([content], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    },

    // Read the content of a file from an input element
    readFileContent: function (inputElement) {
        return new Promise((resolve, reject) => {
            if (!inputElement.files || inputElement.files.length === 0) {
                reject('No file selected');
                return;
            }

            const file = inputElement.files[0];
            const reader = new FileReader();

            reader.onload = function (e) {
                resolve(e.target.result);
            };

            reader.onerror = function () {
                reject('Error reading file');
            };

            reader.readAsText(file);
        });
    }
};
