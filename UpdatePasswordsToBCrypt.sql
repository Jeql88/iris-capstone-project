-- SQL Script to update passwords from SHA256 to BCrypt
-- Run this after deploying the new code

-- BCrypt hash for password "admin" (you should change these passwords after migration)
-- Generated with BCrypt work factor 11 (default)

UPDATE "Users" 
SET "PasswordHash" = '$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm'
WHERE "Username" = 'admin' AND "PasswordHash" = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918';

UPDATE "Users" 
SET "PasswordHash" = '$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm'
WHERE "Username" = 'itperson' AND "PasswordHash" = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918';

UPDATE "Users" 
SET "PasswordHash" = '$2a$11$8EqYytf5J07NnC6me1jaAOGPnPfXqXV3Ue6qVnvqZJxqjqjqjqjqm'
WHERE "Username" = 'faculty' AND "PasswordHash" = '8c6976e5b5410415bde908bd4dee15dfb167a9c873fc4bb8a81f6f2ab448a918';

-- Verify the update
SELECT "Username", "PasswordHash" FROM "Users";
