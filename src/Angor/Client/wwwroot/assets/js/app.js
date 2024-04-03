'use strict';

window.angor = {
    installApp: function () {
        window.AngorAPP.installPWA();
    },

    setTheme: function () {
        const body = document.body;
        const themeIcon = document.getElementById('theme-icon');
        const theme = localStorage.getItem('theme');

        if (theme === 'dark') {
            body.classList.add('dark-version');
            themeIcon.textContent = 'light_mode';
        } else {
            body.classList.remove('dark-version');
            themeIcon.textContent = 'dark_mode';
        }
    },

    showSnackbar: function (message, delaySeconds) {
        const snackbar = document.getElementById('snackbar');
        snackbar.innerHTML = message;
        snackbar.classList.add('show');
        setTimeout(function () { snackbar.classList.remove('show'); }, (delaySeconds * 1000));
    }
};

 



document.addEventListener('DOMContentLoaded', function () {
    window.addEventListener('resize', handleSidenavTypeOnResize);
    window.addEventListener('load', handleSidenavTypeOnResize);
});

function handleSidenavTypeOnResize() {
    const elements = document.querySelectorAll('[onclick="handleSidebarType(this)"]');
    elements.forEach(element => {
        const isDisabled = window.innerWidth < 1200;
        element.classList.toggle('disabled', isDisabled);
    });
}





function toggleSidenav() {
    document.body.classList.toggle('sidenav-pinned');
}

function toggleDarkMode() {
    const body = document.body;
    const themeIcon = document.getElementById('theme-icon');
    const currentTheme = body.classList.contains('dark-version') ? 'light' : 'dark';

    if (currentTheme === 'light') {
        body.classList.remove('dark-version');
        themeIcon.textContent = 'dark_mode';
    } else {
        body.classList.add('dark-version');
        themeIcon.textContent = 'light_mode';
    }

    localStorage.setItem('theme', currentTheme);
}

