open System.Collections.Generic
open System.Data.SQLite
open System.Net.Http
open System.Text
open System.IO

open Donald
open FSharp.Data

type Material = {
    Code: string
    Description: string
    Unit: string
    UnitPrice: decimal
}

let url = "http://www.indexpr.moc.go.th/PRICE_PRESENT/tablecsi_month_region.asp?DDMonth=11&DDYear=2566&DDProvince=10&B1=%B5%A1%C5%A7"            
let materialsHtml = @"c:/temp/Scrape2Api/materials.html" 
let materialsUtfHtml = @"c:/temp/Scrape2Api/materials-utf8.html" 

let request () =
    printfn "Requesting..."
    task {
        
        use file = File.OpenWrite(materialsHtml)

        use client = new HttpClient()
        let formData = 
            [ "DDGroupCode", ""; "Submit", "%B5%A1%C5%A7" ] 
            |> Seq.map (fun (k, v) -> KeyValuePair<string, string>(k, v))
        let content = new FormUrlEncodedContent(formData)

        let! response = client.PostAsync(url, content)
        do! response.Content.CopyToAsync(file)
        return materialsHtml
    }   
    |> Async.AwaitTask
    |> Async.RunSynchronously 

let toUTF8 sourceFilePath =
    printfn "Convert to UTF8..."

    let sourceEncoding = Encoding.GetEncoding(874) // ISO-8859-1 encoding    
    let content = File.ReadAllText(sourceFilePath, sourceEncoding)
    let targetEncoding = Encoding.UTF8 // UTF-8 encoding
    File.WriteAllText(materialsUtfHtml, content, targetEncoding)
    materialsUtfHtml

let toMaterial (tds:string seq) = 
    let getText i = tds |> Seq.item i |> fun x -> x.Trim()

    {
        Code = getText 0
        Description = getText 1
        Unit = getText 2
        UnitPrice = getText 3 |> System.Decimal.Parse  
    }

let parse htmlFile =
    printfn "Parsing..."

    let htmlStream = File.OpenRead(htmlFile)
    let trs = HtmlDocument
                                .Load(htmlStream)
                                .Descendants("tr")

    trs 
    |> Seq.map (fun tr -> tr.Descendants("td") |> Seq.map (fun td -> td.InnerText()))
    |> Seq.filter (fun tds -> tds |> Seq.length |> fun len -> len >= 4)
    |> Seq.filter (fun tds ->                 
                        match tds |> Seq.item 3 |> System.Decimal.TryParse with
                        | isDecimal, _ -> isDecimal)
    |> Seq.map toMaterial

let insertMaterial conn material = 

    let sql = "
        INSERT INTO material (code, description, unit, unit_price)
        VALUES (@code, @description, @unit, @unit_price)
        ON CONFLICT do UPDATE SET description = @description, unit = @unit, unit_price = @unit_price";


    let param = [ 
        "code", sqlString material.Code
        "description", sqlString material.Description
        "unit", sqlString material.Unit
        "unit_price", sqlDecimal material.UnitPrice
    ]

    conn
    |> Db.newCommand sql
    |> Db.setParams param
    |> Db.exec // unit


let saveToDb materials = 
    printfn "Saving to Db..."

    let dbFile = @"c:\temp\Scrape2Api\material.db"
    if not (File.Exists dbFile) then
        let originalDbFile = Path.Combine(System.Environment.CurrentDirectory, "material.db")
        let originalDb = System.IO.FileInfo(originalDbFile)
        do originalDb.CopyTo (dbFile) |> ignore

    use conn = new SQLiteConnection(@"Data Source=c:\temp\Scrape2Api\material.db;Version=3;New=true;")
    materials |> Seq.iter (insertMaterial conn)



let scrape () =
    printfn "Scraping..."

    request ()
    |> toUTF8
    |> parse
    |> saveToDb

    printfn "Done Scraping!"


// Requires for get codepage windows-74
do Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)

async {
    let oneDay = 1000 * 60 * 60 * 24    
    while true do
        do scrape ()
        do! Async.Sleep oneDay
}
|> Async.RunSynchronously