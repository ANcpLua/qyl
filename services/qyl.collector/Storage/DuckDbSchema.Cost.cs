namespace Qyl.Collector.Storage;

public static partial class DuckDbSchema
{
    public const string ModelPricingDdl = """
                                          CREATE TABLE IF NOT EXISTS model_pricing (
                                              provider         VARCHAR NOT NULL,
                                              model            VARCHAR NOT NULL,
                                              input_cost       DECIMAL NOT NULL,
                                              output_cost      DECIMAL NOT NULL,
                                              reasoning_cost   DECIMAL,
                                              cache_read_cost  DECIMAL,
                                              cache_write_cost DECIMAL,
                                              valid_from       TIMESTAMP NOT NULL,
                                              valid_to         TIMESTAMP,
                                              PRIMARY KEY (provider, model, valid_from)
                                          );
                                          """;

    public const string ModelPricingTiersDdl = """
                                               CREATE TABLE IF NOT EXISTS model_pricing_tiers (
                                                   provider       VARCHAR NOT NULL,
                                                   model          VARCHAR NOT NULL,
                                                   tier_name      VARCHAR NOT NULL,
                                                   input_cost     DECIMAL NOT NULL,
                                                   output_cost    DECIMAL NOT NULL,
                                                   reasoning_cost DECIMAL,
                                                   min_tokens     BIGINT,
                                                   valid_from     TIMESTAMP NOT NULL,
                                                   PRIMARY KEY (provider, model, tier_name, valid_from)
                                               );
                                               """;

    public const string CostByModelHourlyViewDdl = """
                                                   CREATE OR REPLACE VIEW cost_by_model_hourly AS
                                                   SELECT
                                                       date_trunc('hour', to_timestamp(start_time_unix_nano / 1000000000)) AS bucket,
                                                       service_name AS service,
                                                       gen_ai_request_model AS model,
                                                       gen_ai_provider_name AS provider,
                                                       COUNT(*) AS call_count,
                                                       SUM(gen_ai_input_tokens) AS total_input_tokens,
                                                       SUM(gen_ai_output_tokens) AS total_output_tokens,
                                                       SUM(gen_ai_cost_usd) AS total_cost
                                                   FROM spans
                                                   WHERE gen_ai_request_model IS NOT NULL
                                                   GROUP BY ALL;
                                                   """;
}
