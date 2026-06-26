window.emojiPickerInterop = {
    showPicker: function (buttonId, dotNetHelper, methodName) {
        let button = document.getElementById(buttonId);
        if (!button) return;

        let existingPicker = document.getElementById("emoji-picker-container-" + buttonId);
        if (existingPicker) {
            existingPicker.remove();
            return;
        }

        let pickerContainer = document.createElement("div");
        pickerContainer.id = "emoji-picker-container-" + buttonId;
        pickerContainer.style.position = "absolute";
        pickerContainer.style.zIndex = "1000";

        let picker = document.createElement("emoji-picker");
        pickerContainer.appendChild(picker);

        document.body.appendChild(pickerContainer);

        // Position it below the button
        let rect = button.getBoundingClientRect();
        pickerContainer.style.top = (rect.bottom + window.scrollY) + "px";
        pickerContainer.style.left = (rect.left + window.scrollX) + "px";

        picker.addEventListener("emoji-click", event => {
            dotNetHelper.invokeMethodAsync(methodName, event.detail.unicode);
            pickerContainer.remove();
        });

        // Close when clicking outside
        let outsideClickListener = (e) => {
            if (!pickerContainer.contains(e.target) && e.target !== button && !button.contains(e.target)) {
                pickerContainer.remove();
                document.removeEventListener('click', outsideClickListener);
            }
        };

        // Delay attaching to prevent immediate trigger
        setTimeout(() => {
            document.addEventListener('click', outsideClickListener);
        }, 10);
    }
};