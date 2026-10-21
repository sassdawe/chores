(function () {
  if (!('serviceWorker' in navigator)) {
    return;
  }

  window.addEventListener('load', function () {
    navigator.serviceWorker.register('/service-worker.js', {
      scope: '/',
      updateViaCache: 'none'
    }).then(function (registration) {
      var queue = self.ChoresOfflineQueue;
      if (!queue) return;

      // Lets the browser retry the queued completions even after every tab is closed.
      return queue.count().then(function (pending) {
        if (pending > 0) {
          return queue.requestBackgroundSync(registration);
        }
      });
    }).catch(function (error) {
      console.warn('Service worker registration failed.', error);
    });
  });
})();
