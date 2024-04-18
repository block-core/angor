<p align="center">
    <img src="https://github.com/miwahn/angor/raw/patch-1/src/Angor/Client/wwwroot/angor-logo.svg" height="100" alt="Angor" />
</p>
<h3 align="center">
    Developer README
</h3>

<br>

## Table of Contents
1. [Installation](#installation)
2. [Contributing](#contributing)
3. [Running Cypress](#running-cypress)

## Installation

To run Angor locally, you need to download .NET 8.0.4 or higher. You can download it from [here](https://dotnet.microsoft.com/en-us/download/dotnet/8.0).

### Steps:
1. Clone the repository:
    ```bash
    $ git clone https://github.com/block-core/angor.git
    ```

2. Navigate to the project directory:
    ```bash
    $ cd angor/src/Angor/Client
    ```

3. Build the project:
    ```bash
    $ dotnet build
    ```

4. Run the project:
    ```bash
    $ dotnet run
    ```

Now you should have Angor running locally at [http://localhost:5062/](http://localhost:5062/).

## Contributing

We welcome contributions to the codebase. Please maintain the same code style as the existing code.

## Running Cypress

To run Cypress, follow these steps:

1. Install dependencies:
    ```bash
    $ npm install
    ```

2. Ensure that Angor is running locally.

3. Open Cypress:
    ```bash
    $ npx cypress open
    ```

