using RetailForge.Generators.Abstractions;

namespace RetailForge.Generators.TestConsole;

public partial class ItemQuery
{
    [GeneratePoco]
    public const string Get = """
        SELECT
            i.sid, i.alu, i.upc, i.item_size, i.attribute,
            i.description, i.description1, i.description2, i.description3, i.description4,
            i.long_description,
            d.d_name, d.d_long_name, d.c_long_name, d.s_long_name,
            i.text1,
            i.udf1_string, i.udf2_string, i.udf3_string, i.udf4_string, i.udf5_string,
            e.udf6_string, e.udf7_string, e.udf8_string, e.udf9_string,
            e.udf10_string, e.udf11_string, e.udf12_string, e.udf13_string,
            e.udf14_string, e.udf15_string,
            v.vend_name AS vendor,
            i.style_sid
        FROM invn_sbs_item i
        LEFT JOIN invn_sbs_extend e ON e.invn_sbs_item_sid = i.sid
        LEFT JOIN dcs d ON d.sid = i.dcs_sid
        LEFT JOIN vendor v ON v.sid = i.vend_sid
        /**where**/
        """;
}
