// The Uno Skia canvas is not a native text editor. Its managed pointer pipeline
// owns focus. A compatibility mouse default must not blur Uno's invisible input
// after that pipeline has already restored it. No keys/text/model are intercepted.
(function (scope) {
    'use strict';
    if (scope.DockingSceneCanvasFocus) return;
    const documents = new WeakMap();
    function install(doc) {
        if (!doc || typeof doc.addEventListener !== 'function') throw new TypeError('A Document is required.');
        if (documents.has(doc)) return documents.get(doc);
        let prevented = 0, disposed = false;
        const onMouse = event => {
            if (disposed || !event.isTrusted || event.defaultPrevented || !event.cancelable || event.button !== 0 || event.detail === 0) return;
            const canvas = event.target, input = doc.activeElement;
            if (canvas?.tagName !== 'CANVAS' || canvas.id !== 'uno-canvas' || canvas.ownerDocument !== doc ||
                input?.id !== 'uno-input' || !['INPUT', 'TEXTAREA'].includes(input.tagName) ||
                input.ownerDocument !== doc || !input.isConnected) return;
            // Do not stop propagation: Uno still receives the complete gesture,
            // including clicks that intentionally focus a toolbar/other control.
            event.preventDefault(); prevented++;
        };
        doc.addEventListener('mousedown', onMouse, true);
        doc.addEventListener('click', onMouse, true);
        const registration = Object.freeze({
            get preventedCount() { return prevented; },
            dispose() {
                if (disposed) return;
                disposed = true;
                doc.removeEventListener('mousedown', onMouse, true);
                doc.removeEventListener('click', onMouse, true);
                documents.delete(doc);
            }
        });
        documents.set(doc, registration);
        return registration;
    }
    scope.DockingSceneCanvasFocus = Object.freeze({ version: 1, install });
    if (scope.document) install(scope.document);
})(globalThis);
