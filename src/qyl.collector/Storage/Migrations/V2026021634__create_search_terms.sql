-- Inverted token index: fast term lookup for search
CREATE TABLE IF NOT EXISTS search_terms (
    term VARCHAR NOT NULL,
    document_id VARCHAR NOT NULL,
    field VARCHAR NOT NULL,
    term_frequency INTEGER NOT NULL DEFAULT 1,
    position_offsets_json JSON,
    PRIMARY KEY (term, document_id, field)
);

CREATE INDEX IF NOT EXISTS idx_search_terms_term ON search_terms(term);
CREATE INDEX IF NOT EXISTS idx_search_terms_document ON search_terms(document_id);
