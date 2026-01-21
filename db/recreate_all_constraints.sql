-- Recreate all constraints (primary keys, unique constraints, foreign keys) after data import
-- This script will recreate all constraints that may have been dropped before import

-- Recreate primary key constraints (if not exists)
-- Note: Primary keys are usually created with the table, but we ensure they exist

-- Recreate unique constraints (using DO block to handle IF NOT EXISTS)
DO $$
BEGIN
    -- Recreate unique constraint for stock_daily_data
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'uk_stock_daily_data' 
        AND conrelid = 'public.stock_daily_data'::regclass
    ) THEN
        ALTER TABLE public.stock_daily_data 
            ADD CONSTRAINT uk_stock_daily_data UNIQUE (stock_code, trade_date);
    END IF;

    -- Recreate unique constraint for stock_exrights_data
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'uk_stock_exrights_data' 
        AND conrelid = 'public.stock_exrights_data'::regclass
    ) THEN
        ALTER TABLE public.stock_exrights_data 
            ADD CONSTRAINT uk_stock_exrights_data UNIQUE (stock_code, ex_rights_date);
    END IF;

    -- Recreate unique constraint for stock_realtime_data
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'uk_stock_realtime_data' 
        AND conrelid = 'public.stock_realtime_data'::regclass
    ) THEN
        ALTER TABLE public.stock_realtime_data 
            ADD CONSTRAINT uk_stock_realtime_data UNIQUE (stock_code);
    END IF;
END $$;

-- Add foreign key constraints (if needed)
-- Uncomment the following DO block if you want to add foreign key relationships:

/*
DO $$
BEGIN
    -- Add foreign key for stock_daily_data
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'fk_stock_daily_data_stock_code'
    ) THEN
        ALTER TABLE public.stock_daily_data
            ADD CONSTRAINT fk_stock_daily_data_stock_code 
            FOREIGN KEY (stock_code) REFERENCES public.stock_info(stock_code);
    END IF;

    -- Add foreign key for stock_exrights_data
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'fk_stock_exrights_data_stock_code'
    ) THEN
        ALTER TABLE public.stock_exrights_data
            ADD CONSTRAINT fk_stock_exrights_data_stock_code 
            FOREIGN KEY (stock_code) REFERENCES public.stock_info(stock_code);
    END IF;

    -- Add foreign key for stock_realtime_data
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'fk_stock_realtime_data_stock_code'
    ) THEN
        ALTER TABLE public.stock_realtime_data
            ADD CONSTRAINT fk_stock_realtime_data_stock_code 
            FOREIGN KEY (stock_code) REFERENCES public.stock_info(stock_code);
    END IF;
END $$;
*/

-- Show constraint creation status
SELECT 
    'Constraints recreated' AS status,
    COUNT(*) AS total_constraints
FROM information_schema.table_constraints 
WHERE constraint_schema = 'public' 
  AND (table_name LIKE 'stock_%' OR table_name = 'data_receive_log')
  AND constraint_type IN ('PRIMARY KEY', 'UNIQUE', 'FOREIGN KEY');
