// Remembers which agenda blocks the user collapsed, in a session cookie scoped to the signed-in user.
(function () {
    'use strict';

    var root = document.querySelector('[data-agenda-state]');
    if (!root) {
        return;
    }

    var cookieName = root.getAttribute('data-agenda-state');
    var cookiePath = root.getAttribute('data-agenda-state-path') || '/';
    var maxLength = 3000;

    if (!cookieName) {
        return;
    }

    function readState() {
        var state = new Map();
        var prefix = cookieName + '=';
        var cookies = document.cookie ? document.cookie.split('; ') : [];

        for (var i = 0; i < cookies.length; i++) {
            if (cookies[i].indexOf(prefix) !== 0) {
                continue;
            }

            var raw = cookies[i].substring(prefix.length);
            var tokens = raw ? raw.split('|') : [];
            for (var j = 0; j < tokens.length; j++) {
                var token = tokens[j];
                if (token.length > 2 && token.charAt(token.length - 2) === ':') {
                    state.set(token.slice(0, -2), token.charAt(token.length - 1) === '1');
                }
            }
            break;
        }

        return state;
    }

    function writeState(state) {
        var tokens = [];
        state.forEach(function (isOpen, key) {
            tokens.push(key + ':' + (isOpen ? '1' : '0'));
        });

        // Drop the oldest toggles rather than let the browser silently reject an oversized cookie.
        var value = tokens.join('|');
        while (value.length > maxLength && tokens.length > 0) {
            tokens.shift();
            value = tokens.join('|');
        }

        var cookie = cookieName + '=' + value + '; path=' + cookiePath + '; samesite=lax';
        if (location.protocol === 'https:') {
            cookie += '; secure';
        }

        document.cookie = cookie;
    }

    var state = readState();

    // "toggle" does not bubble, so listen during the capture phase.
    root.addEventListener('toggle', function (event) {
        var details = event.target;
        if (!details || details.tagName !== 'DETAILS') {
            return;
        }

        var key = details.getAttribute('data-agenda-key');
        if (!key) {
            return;
        }

        state.delete(key);
        state.set(key, details.open);
        writeState(state);
    }, true);
})();
