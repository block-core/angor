'use strict';

(function() {
    const storedTheme = localStorage.getItem('theme');
    if (storedTheme === 'dark') {
        document.body.classList.add('dark');
        const sidebar = document.querySelector('.sidebar');
        if (sidebar) {
            sidebar.classList.remove('white');
        }
    } else {
        document.body.classList.remove('dark');
        const sidebar = document.querySelector('.sidebar');
        if (sidebar) {
            sidebar.classList.add('white');
        }
    }
})();

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
        
        const sidebar = document.querySelector('.sidebar');
        if (sidebar) {
            sidebar.classList.remove('white');
        }
    },

    addLightBackground: function () {
        document.body.classList.remove('dark');
        const loader = document.querySelector('.loader-wrapper');
        if (loader) {
            loader.classList.remove('dark');
        }
        
        const sidebar = document.querySelector('.sidebar');
        if (sidebar) {
            sidebar.classList.add('white');
        }
    },

    initializeSidebar: function () {
        const sidebar = document.querySelector(".sidebar");
        const sidebarToggler = document.querySelector(".sidebar-toggler");
        const menuToggler = document.querySelector(".menu-toggler");
        
        const collapsedSidebarHeight = "56px";
        const fullSidebarHeight = "calc(100vh - 32px)";

        if (sidebarToggler) {
            sidebarToggler.addEventListener("click", () => {
                sidebar.classList.toggle("collapsed");
            });
        }

        const toggleMenu = (isMenuActive) => {
            sidebar.style.height = isMenuActive ? `${sidebar.scrollHeight}px` : collapsedSidebarHeight;
            const span = menuToggler.querySelector("span");
            if (span) {
                span.innerText = isMenuActive ? "close" : "menu";
            }
        };

        if (menuToggler) {
            menuToggler.addEventListener("click", () => {
                toggleMenu(sidebar.classList.toggle("menu-active"));
            });
        }

        window.addEventListener("resize", () => {
            if (window.innerWidth >= 1024) {
                sidebar.style.height = fullSidebarHeight;
            } else {
                sidebar.classList.remove("collapsed");
                sidebar.style.height = "auto";
                toggleMenu(sidebar.classList.contains("menu-active"));
            }
        });
    },

    toggleSidebar: function () {
        const sidebar = document.querySelector(".sidebar");
        if (sidebar) {
            sidebar.classList.toggle("collapsed");
        }
    },

    toggleMenu: function () {
        const sidebar = document.querySelector(".sidebar");
        const menuToggler = document.querySelector(".menu-toggler");
        if (sidebar && menuToggler) {
            const isMenuActive = sidebar.classList.toggle("menu-active");
            sidebar.style.height = isMenuActive ? `${sidebar.scrollHeight}px` : "56px";
            const span = menuToggler.querySelector("span");
            if (span) {
                span.innerText = isMenuActive ? "close" : "menu";
            }
        }
    }
};

 window.scrollToBottom = function(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};

window.preventDefault = function(event) {
    event.preventDefault();
};


