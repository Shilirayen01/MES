window.mesLocalization = {
    getLanguage: () => localStorage.getItem('mes_language'),
    setLanguage: (lang) => localStorage.setItem('mes_language', lang),
    getBrowserLanguage: () => (navigator.language || navigator.userLanguage || 'fr').substring(0, 2).toLowerCase(),
    setDirection: (isRtl) => {
        const html = document.documentElement;
        if (isRtl) {
            html.setAttribute('dir', 'rtl');
            html.setAttribute('lang', 'ar');
        } else {
            html.setAttribute('dir', 'ltr');
        }
    }
};
