<p align="center">
    <img src="https://github.com/miwahn/angor/raw/patch-1/src/Angor/Client/wwwroot/angor-logo.svg" height="100" alt="Angor" />
</p>
<h3 align="center">
    Developer README
</h3>

<br>

## Table of Contents
1) Installation
2) Contributing
3) Cypress


## Installation and running guide 

<h3>To run angor locally, you need to download dotnet 8.0.4 or higher</h3>
<code> https://dotnet.microsoft.com/en-us/download/dotnet/8.0 </code>

<code> $ git clone https://github.com/block-core/angor.git </code>
Enter the project and go to the terminal
<code> cd src\Angor\Client </code>
<code> dotnet build </code>
<code> dotnet run </code>

Now you should have your local host running at http://localhost:5062/

## Contribution 
We welcome contributions to the codebase. Please maintain the same code style as the existing code.


## Running cypress
To run cypress you need to:
1) Go the base of your project and run
<code> npm install </code>

2) Have your angor locally running  
3) Run the command 
<code> npx cypress open </code>
