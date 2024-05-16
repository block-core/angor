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
        document.body.style.backgroundColor = "#022229";
    },

    addLightBackground: function () {
        document.body.style.backgroundColor = "#f4f1ec";
    },

    initSearchFocus: function (searchInputId) {
        document.addEventListener('keydown', function (event) {
            if (event.key === '/') {
                event.preventDefault();
                document.getElementById(searchInputId).focus();
            }
        });
    }
};

// Initialize search focus functionality
window.angor.initSearchFocus('searchQuery');
