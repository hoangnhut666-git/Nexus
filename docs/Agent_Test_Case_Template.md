# Manual Test Case Template Documentation 123

This document defines the standard schema for manual test cases. It is structured specifically for AI agents, automated parsers, and QA personnel to understand the data requirements for each field.

## 1. Schema Definition & Data Dictionary

| Field Name | Data Type | Description & Agent Instructions |
| :--- | :--- | :--- |
| **No.** | `Integer` | Sequential number of the row (e.g., 1, 2, 3). |
| **TC ID** | `String` | Unique identifier for the test case (e.g., `TC_LOGIN_001`). Must be unique across the suite. |
| **Module** | `String` | The overarching system module being tested (e.g., `Authentication`, `Checkout`). |
| **Feature** | `String` | The specific function or sub-module (e.g., `Social Login`, `Payment Gateway`). |
| **Test Scenario** | `String` | High-level business flow or use case being verified (e.g., `Verify successful login with valid credentials`). |
| **Pre-condition** | `String` | System state or prerequisites required before execution (e.g., `User account must be registered and active`). |
| **Test Steps** | `Text` | Numbered, step-by-step instructions to execute the test. |
| **Test Data** | `Text` | Specific inputs, variables, or credentials required for the test steps. |
| **Expected Result** | `Text` | The definitive correct system behavior if the feature works as intended. |
| **Actual Result (Round 1)** | `Text` | The observed system behavior during the first testing round. |
| **Status (Round 1)** | `Enum` | Acceptable values: `Pass`, `Fail`, `Blocked`, `N/A`, `Untested`. |
| **Tester (Round 1)** | `String` | Name or ID of the agent/person executing the first round. |
| **Test Date (Round 1)** | `Date` | The date of Round 1 execution in `YYYY-MM-DD` format. |
| **Bug ID (Round 1)** | `String` | Issue tracker ID if the test failed in Round 1 (e.g., `BUG-123`). Leave blank if Pass. |
| **Actual Result (Round 2)** | `Text` | The observed system behavior during the re-test (Round 2). |
| **Status (Round 2)** | `Enum` | Acceptable values: `Pass`, `Fail`, `Blocked`, `N/A`, `Untested`. |
| **Tester (Round 2)** | `String` | Name or ID of the agent/person executing the second round. |
| **Test Date (Round 2)** | `Date` | The date of Round 2 execution in `YYYY-MM-DD` format. |
| **Bug ID (Round 2)** | `String` | Issue tracker ID if the test failed in Round 2. |
| **Priority** | `Enum` | Priority of the logged bug. Values: `Low`, `Medium`, `High`, `Critical`. |
| **Severity** | `Enum` | Severity of the logged bug. Values: `Minor`, `Major`, `Critical`, `Fatal`. |
| **Notes** | `Text` | Additional comments, context, or environmental constraints (e.g., `Tested on Chrome v115`). |

## 2. Blank Markdown Table Format

Agents can use the following table structure to generate test cases in markdown:

| No. | TC ID | Module | Feature | Test Scenario | Pre-condition | Test Steps | Test Data | Expected Result | Actual Result (Round 1) | Status (Round 1) | Tester (Round 1) | Test Date (Round 1) | Bug ID (Round 1) | Actual Result (Round 2) | Status (Round 2) | Tester (Round 2) | Test Date (Round 2) | Bug ID (Round 2) | Priority | Severity | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | | | | | | | | | | | | | | | | | | | | | |

## 3. Agent Instructions for Generation

- **Clarity:** Ensure `Test Steps` and `Expected Result` are highly explicit.
- **Completeness:** Do not leave `Test Scenario` or `Pre-condition` empty unless absolutely no setup is required.
- **Format:** Use bullet points or numbered lists inside `Test Steps` if generating Markdown or JSON payloads based on this schema.
