module Api.Program

open Falco
open Falco.Routing
open Falco.HostBuilder

open Donald
open System.Data
open System.Data.SQLite

type Material = {
    Code: string
    Description: string
    Unit: string
    UnitPrice: decimal
}

module Material =
   let ofDataReader (rd : IDataReader) : Material =
       { Code = rd.ReadString "code"
         Description = rd.ReadString "description"
         Unit = rd.ReadString "unit"
         UnitPrice = rd.ReadDecimal "unit_price" }

let getAllMaterials () =
    use conn = new SQLiteConnection(@"Data Source=C:\temp\Scrape2Api\material.db;Version=3;New=true;")

    let sql = "SELECT code, description, unit, unit_price FROM material"
    
    conn
    |> Db.newCommand sql
    |> Db.query Material.ofDataReader
    |> Response.ofJson


[<EntryPoint>]
let main args =
    webHost args {
        endpoints [
            get "/materials" (getAllMaterials ())
        ]
    }
    0