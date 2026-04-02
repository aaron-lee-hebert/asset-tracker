# Asset Tracker

A practice project for raw SQL fluency, Dapper patterns, connection management, and schema design.

## Learning Objectives

- Raw SQL fluency by writing schema migrations, queries, and inserts by hand without an ORM safety net
- Introducing Dapper patterns via parameter mapping, multi-mapping, query vs execute and handling nulls gracefully
- Understanding connection management by opening, disposing and scopping `SqlConnection` correct wiout an ORM's session abstraction
- Schmea design by modeling balance history as an append-only ledger vs. mutable records
- Revisting the console app architecture and keeping a CLI app from turning into a ball of mud
- Simple command dispatch without using a framework
