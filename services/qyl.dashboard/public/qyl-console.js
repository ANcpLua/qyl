
(function () {
    'use strict';
    if (window.__qylConsole) return; // Already loaded
    window.__qylConsole = true;

    const cfg = {
        endpoint: document.currentScript?.dataset?.endpoint || 'http://localhost:5100',
        sessionId: document.currentScript?.dataset?.session || null,
        batch: true, batchMs: 100, maxQueue: 50
    };

    const queue = [];
    let timer = null;
    const orig = {log: console.log, info: console.info, warn: console.warn, error: console.error, debug: console.debug};

    function send(level, args) {
        const msg = args.map(a => typeof a === 'object' ? JSON.stringify(a) : String(a)).join(' ');
        const entry = {level, message: msg, sessionId: cfg.sessionId, url: location.href};

        if (level === 'error') {
            try {
                throw new Error();
            } catch (e) {
                entry.stack = e.stack?.split('\n').slice(3).join('\n');
            }
        }

        if (cfg.batch) {
            queue.push(entry);
            if (queue.length >= cfg.maxQueue) flush();
            else if (!timer) timer = setTimeout(flush, cfg.batchMs);
        } else {
            post([entry]);
        }
    }

    function flush() {
        if (timer) {
            clearTimeout(timer);
            timer = null;
        }
        if (!queue.length) return;
        const batch = queue.splice(0, cfg.maxQueue);
        post(batch);
    }

    function post(logs) {
        // A failed export must not recurse through the patched console.
        try {
            fetch(cfg.endpoint + '/api/v1/console', {
                method: 'POST', headers: {'Content-Type': 'application/json'},
                body: JSON.stringify({logs}), keepalive: true
            }).catch(() => {
            }); // Silently fail - don't log our own errors
        } catch {
        }
    }

    ['debug', 'log', 'info', 'warn', 'error'].forEach(level => {
        console[level] = function (...args) {
            orig[level].apply(console, args);
            send(level, args);
        };
    });

    window.addEventListener('error', e => {
        send('error', [`Uncaught: ${e.message} at ${e.filename}:${e.lineno}`]);
    });
    window.addEventListener('unhandledrejection', e => {
        send('error', [`Unhandled rejection: ${e.reason}`]);
    });

    window.addEventListener('beforeunload', flush);

    window.QylConsole = {
        init: opts => Object.assign(cfg, opts),
        setSession: id => cfg.sessionId = id,
        flush
    };
})();
