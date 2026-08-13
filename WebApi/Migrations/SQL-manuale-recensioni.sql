-- =====================================================================
-- Aggiunge le colonne per le recensioni scritte alla tabella Clasification
-- =====================================================================
--
-- DOVE LANCIARLO:
--   Supabase -> il tuo progetto -> SQL Editor -> New query -> incolla -> Run
--
-- PERCHE' A MANO E NON CON UNA MIGRATION EF:
--   Le migration nel repo non corrispondono al database reale (la tabella
--   page_visits e' stata creata a mano, con nomi di colonna diversi da quelli
--   che genererebbe EF). Lanciare "dotnet ef database update" rischierebbe di
--   creare tabelle doppie o di fallire a metà. Due ALTER TABLE sono piu'
--   sicuri e si vedono subito.
--
-- E' SICURO RILANCIARLO:
--   "IF NOT EXISTS" fa sì che eseguirlo due volte non dia errore.
--
-- COSA FA:
--   Review    -> il testo libero della recensione (puo' restare vuoto: chi
--                vuole vota e basta, senza scrivere)
--   CreatedAt -> quando e' stata scritta, per mostrarle in ordine
-- =====================================================================

ALTER TABLE "Clasification"
    ADD COLUMN IF NOT EXISTS "Review" text;

ALTER TABLE "Clasification"
    ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone
    NOT NULL DEFAULT now();


-- Rende veloce l'elenco delle recensioni di un singolo mostro,
-- che e' la query che girera' a ogni apertura del modale.
CREATE INDEX IF NOT EXISTS "IX_Clasification_Monster_CreatedAt"
    ON "Clasification" ("Monster", "CreatedAt" DESC);


-- =====================================================================
-- VERIFICA: lancia anche questa e controlla di vedere Review e CreatedAt
-- =====================================================================
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_name = 'Clasification'
ORDER BY ordinal_position;
