export function toggleTheme()
{
    const theme = localStorage.getItem('theme') ??
        (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

    if (theme == 'light')
    {
        document.documentElement.classList.add('dark');
        localStorage.theme = 'dark';
    }
    else
    {
        document.documentElement.classList.remove('dark');
        localStorage.theme = 'light';
    }
}