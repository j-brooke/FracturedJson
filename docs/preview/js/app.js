/* Sets the selected range of text in the given textarea */
function setTextareaSelection(elementId, start, end) {
    const textarea = document.getElementById(elementId);
    if (textarea) {
        textarea.setSelectionRange(start, end);
        textarea.focus();
    }
}
