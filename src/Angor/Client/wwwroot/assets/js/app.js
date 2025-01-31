'use strict';

window.angor = {
    installApp: function () {
        window.AngorAPP.installPWA();
    },

    showSnackbar: function (message, delaySeconds) {
        const snackbar = document.getElementById('snackbar');
        snackbar.innerHTML = message;
        snackbar.classList.add('show');
        setTimeout(function () { snackbar.classList.remove('show'); }, (delaySeconds * 1000));
    },

    addDarkBackground: function () {
        document.body.classList.add('dark');
        const loader = document.querySelector('.loader-wrapper');
        if (loader) {
            loader.classList.add('dark');
        }
    },

    addLightBackground: function () {
        document.body.classList.remove('dark');
        const loader = document.querySelector('.loader-wrapper');
        if (loader) {
            loader.classList.remove('dark');
        }
    }
};
