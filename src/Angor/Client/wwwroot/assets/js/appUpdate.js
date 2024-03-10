var angorBrowse = window.angorBrowse || {};
angorBrowse.mainBodyInstance = null;

const bc = new BroadcastChannel('angor-channel');
bc.onmessage = function (message) {
    if (message && message.data == "new-version-found") {
        notifyAppUpdateToUser();
    } else if (message && message.data == "reload-page") {
        setTimeout(function () {
            //location.reload();
            window.location.href = window.location.href;
        }, 500);
    }
}

function notifyAppUpdateToUser() {
    setTimeout(function () {
        if (angorBrowse.mainBodyInstance) {
            angorBrowse.mainBodyInstance.invokeMethodAsync('ShowUpdateVersion').then(function () { }, function (er) {
                 setTimeout(notifyAppUpdateToUser, 5000);
            });
        }
    }, 2000);
}
angorBrowse.onUserUpdate = function () {
    setTimeout(function () {
        bc.postMessage("skip-waiting");
    }, 300);
}

var mainBodyInstance;
function setRef(ref) {
    angorBrowse.mainBodyInstance = ref;
}