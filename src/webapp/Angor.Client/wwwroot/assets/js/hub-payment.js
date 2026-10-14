window.angorPayment = {
    setTheme(theme) {
        document.documentElement.dataset.paymentTheme = theme;
        const url = new URL(location.href);
        url.searchParams.set('theme', theme);
        history.replaceState(history.state, '', url);
    },
    openMenu(dialog) {
        if (!dialog.dataset.dismissBound) {
            dialog.addEventListener('click', event => {
                const bounds = dialog.getBoundingClientRect();
                if (event.target === dialog && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) dialog.close();
            });
            dialog.dataset.dismissBound = 'true';
        }
        dialog.showModal();
    },
    closeMenu(dialog) { dialog.close(); }
};
