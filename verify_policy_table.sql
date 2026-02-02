-- Verify the updated Policies table structure
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns 
WHERE table_name = 'Policies' 
    AND table_schema = 'public'
ORDER BY ordinal_position;

-- Show sample data if any exists
SELECT * FROM "Policies" LIMIT 5;