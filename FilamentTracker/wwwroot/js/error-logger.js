// Client-side error logger: send basic error data to server and write to console
(function () {
    function send(payload) {
        try {
            var blob = new Blob([JSON.stringify(payload)], { type: 'application/json' });
            if (navigator.sendBeacon) {
                navigator.sendBeacon('/client-error', blob);
            } else {
                fetch('/client-error', { method: 'POST', body: JSON.stringify(payload), headers: { 'Content-Type': 'application/json' } }).catch(function () { });
            }
        } catch (e) { console.error('error-logger send failed', e); }
    }

    window.addEventListener('error', function (e) {
        try {
            var payload = {
                type: 'error',
                message: e.message || null,
                filename: e.filename || null,
                lineno: e.lineno || null,
                colno: e.colno || null,
                stack: (e.error && e.error.stack) || null
            };
            console.error('window.onerror', payload);
            send(payload);
        } catch (ex) { console.error(ex); }
    });

    window.addEventListener('unhandledrejection', function (e) {
        try {
            var payload = {
                type: 'unhandledrejection',
                reason: (e.reason && e.reason.stack) || e.reason || null
            };
            console.error('unhandledrejection', payload);
            send(payload);
        } catch (ex) { console.error(ex); }
    });
})();
