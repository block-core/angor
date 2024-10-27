const colors = require('tailwindcss/colors');

module.exports = {
    content: [
        '!**/{bin,obj,node_modules}/**',
        '**/*.{razor,html}',
    ],
    theme:
    {
        extend:
        {
            colors: {
                'angor-primary': '#022229',
                'angor-secondary': '#086c81',
                'angor-accent': '#cbdde1'
            },
            animation: {
                'spin-slow': 'spin 7s linear infinite',
            }
        }
    },
    darkMode: 'class',
    plugins: [
        require('tailwindcss-debug-screens')
    ]
}