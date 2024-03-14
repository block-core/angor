window.angor = {
    installApp: function () {
        window.AngorAPP.installPWA();
    },

    setTheme: function () {
        const body = document.getElementsByTagName('body')[0];
        const theme = localStorage.getItem('theme');
        const themeIcon = document.getElementById('theme-icon');

        if (theme === 'dark') {
            body.classList.add('dark-version');
            themeIcon.textContent = 'light_mode'; // Change icon to light mode
        } else {
            body.classList.remove('dark-version');
            themeIcon.textContent = 'dark_mode'; // Change icon to dark mode
        }
    }
};



