"use strict";

window.angor = {
    installApp: function () {
        window.AngorAPP.installPWA();
    },

    setTheme: function () {
        const body = document.querySelector('body');
        const hr = document.querySelectorAll('div:not(.sidenav) > hr');
        const hr_card = document.querySelectorAll('div:not(.bg-gradient-dark) hr');
        const text_btn = document.querySelectorAll('button:not(.btn) > .text-dark');
        const text_span = document.querySelectorAll('span.text-dark, .breadcrumb .text-dark');
        const text_span_white = document.querySelectorAll('span.text-white, .breadcrumb .text-white');
        const text_strong = document.querySelectorAll('strong.text-dark');
        const text_strong_white = document.querySelectorAll('strong.text-white');
        const text_nav_link = document.querySelectorAll('a.nav-link.text-dark');
        const text_nav_link_white = document.querySelectorAll('a.nav-link.text-white');
        const secondary = document.querySelectorAll('.text-secondary');
        const bg_gray_100 = document.querySelectorAll('.bg-gray-100');
        const bg_gray_600 = document.querySelectorAll('.bg-gray-600');
        const btn_text_dark = document.querySelectorAll('.btn.btn-link.text-dark, .material-icons.text-dark');
        const btn_text_white = document.querySelectorAll('.btn.btn-link.text-white, .material-icons.text-white');
        const card_border = document.querySelectorAll('.card.border');
        const card_border_dark = document.querySelectorAll('.card.border.border-dark');
        const svg = document.querySelectorAll('g');

        const theme = localStorage.getItem('theme');
        const themeIcon = document.getElementById('theme-icon');

        if (theme === 'dark') {
            body.classList.add('dark-version');
            themeIcon.textContent = 'light_mode';

            for (let i = 0; i < hr.length; i++) {
                if (hr[i].classList.contains('dark')) {
                    hr[i].classList.remove('dark');
                    hr[i].classList.add('light');
                }
            }

            for (let i = 0; i < hr_card.length; i++) {
                if (hr_card[i].classList.contains('dark')) {
                    hr_card[i].classList.remove('dark');
                    hr_card[i].classList.add('light');
                }
            }

            for (let i = 0; i < text_btn.length; i++) {
                if (text_btn[i].classList.contains('text-dark')) {
                    text_btn[i].classList.remove('text-dark');
                    text_btn[i].classList.add('text-white');
                }
            }

            for (let i = 0; i < text_span.length; i++) {
                if (text_span[i].classList.contains('text-dark')) {
                    text_span[i].classList.remove('text-dark');
                    text_span[i].classList.add('text-white');
                }
            }

            for (let i = 0; i < text_strong.length; i++) {
                if (text_strong[i].classList.contains('text-dark')) {
                    text_strong[i].classList.remove('text-dark');
                    text_strong[i].classList.add('text-white');
                }
            }

            for (let i = 0; i < text_nav_link.length; i++) {
                if (text_nav_link[i].classList.contains('text-dark')) {
                    text_nav_link[i].classList.remove('text-dark');
                    text_nav_link[i].classList.add('text-white');
                }
            }

            for (let i = 0; i < secondary.length; i++) {
                if (secondary[i].classList.contains('text-secondary')) {
                    secondary[i].classList.remove('text-secondary');
                    secondary[i].classList.add('text-white', 'opacity-8');
                }
            }

            for (let i = 0; i < bg_gray_100.length; i++) {
                if (bg_gray_100[i].classList.contains('bg-gray-100')) {
                    bg_gray_100[i].classList.remove('bg-gray-100');
                    bg_gray_100[i].classList.add('bg-gray-600');
                }
            }

            btn_text_dark.forEach(element => {
                element.classList.remove('text-dark');
                element.classList.add('text-white');
            });

            svg.forEach(element => {
                if (element.hasAttribute('fill')) {
                    element.setAttribute('fill', '#fff');
                }
            });

            card_border.forEach(element => {
                element.classList.add('border-dark');
            });

        } else {
            body.classList.remove('dark-version');

            hr.forEach(element => {
                if (element.classList.contains('light')) {
                    element.classList.add('dark');
                    element.classList.remove('light');
                }
            });

            hr_card.forEach(element => {
                if (element.classList.contains('light')) {
                    element.classList.add('dark');
                    element.classList.remove('light');
                }
            });

            text_btn.forEach(element => {
                if (element.classList.contains('text-white')) {
                    element.classList.remove('text-white');
                    element.classList.add('text-dark');
                }
            });

            text_span_white.forEach(element => {
                if (element.classList.contains('text-white') && !element.closest('.sidenav') && !element.closest('.card.bg-gradient-dark')) {
                    element.classList.remove('text-white');
                    element.classList.add('text-dark');
                }
            });

            text_strong_white.forEach(element => {
                if (element.classList.contains('text-white')) {
                    element.classList.remove('text-white');
                    element.classList.add('text-dark');
                }
            });

            text_nav_link_white.forEach(element => {
                if (element.classList.contains('text-white') && !element.closest('.sidenav')) {
                    element.classList.remove('text-white');
                    element.classList.add('text-dark');
                }
            });

            secondary.forEach(element => {
                if (element.classList.contains('text-white')) {
                    element.classList.remove('text-white', 'opacity-8');
                    element.classList.add('text-dark');
                }
            });

            bg_gray_600.forEach(element => {
                if (element.classList.contains('bg-gray-600')) {
                    element.classList.remove('bg-gray-600');
                    element.classList.add('bg-gray-100');
                }
            });

            svg.forEach(element => {
                if (element.hasAttribute('fill')) {
                    element.setAttribute('fill', '#252f40');
                }
            });

            btn_text_white.forEach(element => {
                if (!element.closest('.card.bg-gradient-dark')) {
                    element.classList.remove('text-white');
                    element.classList.add('text-dark');
                }
            });

            card_border_dark.forEach(element => {
                element.classList.remove('border-dark');
            });

            themeIcon.textContent = 'dark_mode';
        }
    },

    showSnackbar: function (message, delaySeconds) {
        var x = document.getElementById("snackbar");
        x.innerHTML = message;
        x.className = "show";
        setTimeout(function () { x.className = x.className.replace("show", ""); }, (delaySeconds * 1000));
    }
};
