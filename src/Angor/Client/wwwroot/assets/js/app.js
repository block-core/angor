"use strict";

window.angor = {
    installApp: function () {
        window.AngorAPP.installPWA();
    },

    setTheme: function () {
        const body = document.getElementsByTagName('body')[0];
        const themeIcon = document.getElementById('theme-icon');
        const theme = localStorage.getItem('theme');

        if (theme === 'dark') {
            body.classList.add('dark-version');
            themeIcon.textContent = 'light_mode'; 
            localStorage.setItem('theme', 'dark');
        }  
    },

    showSnackbar: function (message, delaySeconds) {
        var x = document.getElementById("snackbar");
        x.innerHTML = message;
        x.className = "show";
        setTimeout(function () { x.className = x.className.replace("show", ""); }, (delaySeconds * 1000));
    }
};
