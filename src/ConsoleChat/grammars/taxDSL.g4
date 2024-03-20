grammar taxDSL;

// Parser rules
program: (taxCalculation | deductionApplication | creditCalculation)+;

taxCalculation: 'calculate_tax' '{' incomeClause filingStatusClause taxYearClause '}';
deductionApplication: 'apply_deductions' '{' incomeClause deductionsClause '}';
creditCalculation: 'calculate_credits' '{' incomeClause creditsClause filingStatusClause '}';

incomeClause: 'income:' DOLLAR_AMOUNT;
filingStatusClause: 'filing_status:' ( 'single' | 'joint' | 'married_filing_separately' | 'head_of_household' );
taxYearClause: 'tax_year:' YEAR;
deductionsClause: 'deductions:' '{' (standardDeduction | namedDeduction)+ '}';
creditsClause: 'credits:' '{' (namedCredit)+ '}';

standardDeduction: 'standard:' DOLLAR_AMOUNT;
namedDeduction: IDENTIFIER ':' DOLLAR_AMOUNT;
namedCredit: IDENTIFIER ':' (DOLLAR_AMOUNT | 'children' INT);

// Lexer rules
DOLLAR_AMOUNT: '$' [0-9]+ (',' [0-9]{3})*;
YEAR: [0-9]{4};
IDENTIFIER: [a-zA-Z_][a-zA-Z_0-9]*;
INT: [0-9]+;
WS: [ \t\r\n]+ -> skip;
