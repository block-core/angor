const serviceWorkerFileName = 'service-worker.js';
const swInstalledEvent = 'installed';
const staticCachePrefix = 'angor-cache-v';
const blazorAssembly = 'Angor';
const blazorInstallMethod = 'PWAInstallable';

const notifyNewVersion = () => {
    const bc = new BroadcastChannel('angor-channel');
    bc.postMessage('skip-waiting');  
    bc.onmessage = (message) => {
        if (message && message.data === "reload-page") {
            location.reload();
        }
    };
};


window.updateAvailable = new Promise(function (resolve, reject) {
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register(serviceWorkerFileName)
            .then(function (registration) {
                console.log('Registration successful, scope is:', registration.scope);
                registration.onupdatefound = () => {
                    const installingWorker = registration.installing;
                    installingWorker.onstatechange = () => {
                        switch (installingWorker.state) {
                            case swInstalledEvent:
                                if (navigator.serviceWorker.controller) {
                                    resolve(true);
                                } else {
                                    resolve(false);
                                }
                                break;
                            default:
                        }
                    };
                };
            })
            .catch(error =>
                console.log('Service worker registration failed, error:', error));
    }
});
window.updateAvailable
    .then(isAvailable => {
        if (isAvailable) {
            window.isUpdateAvailable = true;
            const updateButton = document.getElementById('updateButton');
            if (updateButton) {
                updateButton.classList.remove('d-none');
            }
        }
    });

function showAddToHomeScreen() {
    const installButton = document.getElementById('installButton');
    if (installButton) {
        installButton.classList.remove('d-none');
    }
}

window.addEventListener('beforeinstallprompt', function (e) {
    e.preventDefault();
    window.PWADeferredPrompt = e;
    showAddToHomeScreen();
});


window.AngorAPP = {
    installPWA: function () {
        if (window.PWADeferredPrompt) {
            window.PWADeferredPrompt.prompt();
            window.PWADeferredPrompt.userChoice
                .then(function (choiceResult) {
                    window.PWADeferredPrompt = null;
                });
            const installButton = document.getElementById('installButton');
            if (installButton) {
                installButton.classList.add('d-none');
            }
        }
    }
};