
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace HdbMcp.Server.Resources;

[McpServerResourceType]
public static class SemanticsResource
{
    [McpServerResource(UriTemplate = "schema://semantics",
                       Name = "Schema semantics",
                       MimeType = "text/markdown")]
    [Description("What the columns actually mean, and the six ways a naive " +
        "query over this data produces a confident wrong answer.")]
    public static string Semantics() => Content;

    private const string Content = """
        # HDB resale transactions: what the columns mean

        988,123 rows, Jan 1990 to the present, unioned from five published
        datasets. Snapshot pinned; see data/snapshot-2026-10-05.

        ## 1. The date basis changed in March 2012
        Rows before 2012-03 are dated by APPROVAL date. From 2012-03 onward
        they are dated by REGISTRATION date. These are different events,
        weeks apart. Any figure spanning the change compares two different
        things. Every row carries date_basis so the mixture is visible.
        price_summary refuses ranges that cross it.

        ## 2. remaining_lease is absent, then numeric, then a string
        The oldest datasets do not have it. One vintage stores a plain year
        count. From 2017 it is a string like "61 years 04 months". It is
        normalised to remaining_lease_years (decimal) and the original is
        kept in remaining_lease_raw for audit.

        ## 3. storey_range is a bucket, not a number
        Values look like "10 TO 12". storey_min and storey_max are derived,
        but averaging a bucket midpoint is an assumption, not a measurement.
        Any tool that does it must say so.

        ## 4. resale_price is nominal
        The actual dollars paid at the time. Prices in 1990 are in 1990
        dollars. Use price_trend with realTerms for constant 2024 dollars.
        CPI is annual, All Items, 2024 = 100, from SingStat, ending 2025.

        ## 5. flat_model is long-tailed and historically inconsistent
        Model names appear and disappear across eras. Comparing a model
        across decades often compares availability, not price.

        ## 6. The data is five datasets, not one
        source_dataset is on every row. Coverage boundaries are real seams,
        not arbitrary.

        ## What resale price does not include
        It is the transacted price only. Not Cash Over Valuation as a
        separate figure, not agent fees, not renovation.
        """;
}