-- Runs once, when the PostgreSQL volume is first created.
-- pgvector backs skill and post matching later on; the test database lets
-- integration tests run against the same server without touching dev data.
CREATE EXTENSION IF NOT EXISTS vector;

CREATE DATABASE circleshift_test;
\connect circleshift_test
CREATE EXTENSION IF NOT EXISTS vector;
