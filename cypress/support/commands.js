
Cypress.Commands.add('visitLocalhost', () => {
    // Set the viewport to a desktop resolution
    cy.viewport(1280, 720); // You can adjust the width and height as needed
    cy.visit('http://localhost:5062/');
    // cy.log('Hello world');
    console.log('hello world');
    cy.wait(10000);
    // cy.get('.loader-wrapper').should('not.exist'); // Wait for loader to disappear
    // cy.get('.loader-wrapper', { timeout: 30000 }).should('not.exist');
    // cy.document().then(doc => {
    //     cy.log('All elements on the page:');
    //     cy.log(doc.body.outerHTML);
    //   });
    // waitForWasmLoading();
    cy.log('Finished waiting wasm loading');
    cy.get('span[role="button"].material-icons.opacity-10.btn-angor.fs-3#theme-icon').should('be.visible').click(); // Interact with the theme icon
}); 

const waitForWasmLoading = () => {
    return cy.window().then(win => {
      return new Cypress.Promise(resolve => {
        const checkWasmLoaded = () => {
          if (win.WebAssembly.instantiateStreaming) {
            resolve(); // WebAssembly has finished loading
          } else {
            setTimeout(checkWasmLoaded, 100); // Check again after a short delay
          }
        };
        checkWasmLoaded();
      });
    });
  };

Cypress.Commands.add('clickOnNavBar', (dir) => {
    cy.get(`[href="${dir}"]`).should('be.visible').click();
});

Cypress.Commands.add('clickOnButtonContain', (name) => {
    cy.log(name)
    cy.get('.btn.btn-primary')
        .contains(`${name}`)
        .filter(':visible') // Filter to only visible elements
        .click(); // Click on it
})

Cypress.Commands.add('clickElementWithDataCy', (datacy) => {
    cy.get(`[data-cy=${datacy}]`).click();
})


Cypress.Commands.add("clickCardContains", (name, msg) => {
    cy.get('.card-body[role="button"]').contains(`${name}`).click();
    cy.log(msg)
    if(msg){
        cy.get('.snackbar').should('be.visible'); // Assuming snackbar has a class 'snackbar'
        cy.get('.snackbar').contains(msg); // Assuming snackbar contains text
    }
  })
  



Cypress.Commands.add('clickSubmitButton', (msg) => {
    cy.get('.btn.btn-success').click();
    if(msg){
        cy.get('#snackbar').should('be.visible'); // Assuming snackbar has a class 'snackbar'
        cy.get('#snackbar').contains(msg); // Assuming snackbar contains text
    }
})

Cypress.Commands.add('typeTextInElement', (type,msg) => {
    cy.get(`.input-group input[type=${type}]`)
        .type(msg);
})

Cypress.Commands.add('clickOnCheckBox', () => {
    cy.get(`.form-check input[type="checkbox"]`)
  .check();
})

Cypress.Commands.add('clickOnCheckBoxByDataCy', (datacy) => {
    cy.get(`[data-cy=${datacy}]`).check();
})

Cypress.Commands.add('waitForLoader', () => {
    cy.get('.loader').should('not.exist');
})

Cypress.Commands.add('verifyBalance', (num,datacy) => {
    cy.get(`[data-cy=${datacy}]`)
        .should('be.visible')
        .contains(`${num}`);
})


