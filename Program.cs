using System;
using Microsoft.Data.Sqlite;

namespace csharp_sqlite
{
    class Program
    {
        static void Main(string[] args)
        {
            // VERBINDUNG HERSTELLEN
            var db = new SqliteConnection("Data Source=game.db");
            db.Open();


            // INVENTAR TABELLE ANLEGEN

            // erstellen eines sql befehls für die datenbank
            var createInventory = db.CreateCommand();

            // @ heißt, dass alle escape sequenzen ignoriert werden ( alles mit \). notwendig für mehrzeilige strings.
            createInventory.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Inventar (                             
                    InventarID INTEGER PRIMARY KEY,
                    Gold INTEGER DEFAULT 0 CHECK (Gold >= 0)
                );
            ";
            createInventory.ExecuteNonQuery();


            // INVENTAR EINTRÄGE HINZUFÜGEN

            var insertInventory = db.CreateCommand();
            insertInventory.CommandText =
            @"
                INSERT INTO Inventar VALUES (1, 100);
                INSERT INTO Inventar VALUES (2, 200);
            ";
            var numRowsAdded = insertInventory.ExecuteNonQuery();
            Console.WriteLine($"Es wurde {numRowsAdded} Datensätze hinzugefügt");


            // INVENTAR EINTRAG HINZUFÜGEN DER NEGATIVES GOLD HAT

            var insertInventoryNegativeGold = db.CreateCommand();
            insertInventoryNegativeGold.CommandText =
            @"
                INSERT INTO Inventar (Gold) VALUES (-1);
            ";

            try
            {
                insertInventoryNegativeGold.ExecuteNonQuery();
            }
            catch (Microsoft.Data.Sqlite.SqliteException e)
            {
                Console.WriteLine("Hinzufügen von Inventar Datensatz fehlgeschlagen: " + e.Message);
            }

            // WEITERE TABELLEN ANLEGEN

            var addMoreTablesCommand = db.CreateCommand();
            addMoreTablesCommand.CommandText =
            @"
                CREATE TABLE IF NOT EXISTS Items (                             
                    ItemID INTEGER PRIMARY KEY,
                    ItemName VARCHAR(1024) UNIQUE
                );

                INSERT INTO Items (ItemName) VALUES ('Schwert');
                INSERT INTO Items (ItemName) VALUES ('Dolch');

                CREATE TABLE IF NOT EXISTS Inventar_Hat (
                    InventarID INTEGER,
                    ItemID INTEGER,
                    FOREIGN KEY (InventarID) REFERENCES Inventar(InventarID),
                    FOREIGN KEY (ItemID) REFERENCES Items(ItemID)
                );

                INSERT INTO Inventar_Hat VALUES (1, 1);
                INSERT INTO Inventar_Hat VALUES (1, 2);
                INSERT INTO Inventar_Hat VALUES (2, 1);
            ";
            addMoreTablesCommand.ExecuteNonQuery();

            // VERSUCHEN EIN ITEM HINZUZUFÜGEN DAS NICHT EXISTIERT

            var addInvalidItemCommand = db.CreateCommand();
            addInvalidItemCommand.CommandText =
            @"
                INSERT INTO Inventar_Hat VALUES (1, 3);
            ";

            try
            {
                addInvalidItemCommand.ExecuteNonQuery();
            }
            catch (Microsoft.Data.Sqlite.SqliteException e)
            {
                Console.WriteLine("Hinzufügen von Item in Inventar fehlgeschlagen: " + e.Message);
            }


            // TRANSAKTION FÜR FEHLGESCHLAGENEN KAUF EINES ITEMS

            var failedTransactionCommand = db.CreateCommand();
            failedTransactionCommand.CommandText =
            @"
                INSERT INTO Items (ItemName) VALUES ('Kultisten Dolch');

                BEGIN TRANSACTION;

                INSERT INTO Inventar_Hat VALUES (1, 3);

                UPDATE Inventar SET gold = gold - 200 WHERE InventarID = 1;
            ";
            try
            {
                failedTransactionCommand.ExecuteNonQuery();
            }
            catch (Microsoft.Data.Sqlite.SqliteException e)
            {
                Console.WriteLine("Kauf fehlgeschlagen: " + e.Message);

                // die statements oben wurden noch nicht in die datenbank geschrieben
                // stattdesses wurde ein journal angelegt, dieses wird mit ROLLBACK gelöscht

                var rollbackCommand = db.CreateCommand();
                rollbackCommand.CommandText = "ROLLBACK;";
                rollbackCommand.ExecuteNonQuery();
            }

            // TRANSAKTION FÜR KAUF EINES ITEMS

            var transactionCommand = db.CreateCommand();
            transactionCommand.CommandText =
            @"
                UPDATE Inventar SET GOLD = 250 WHERE InventarID = 1;

                BEGIN TRANSACTION;

                INSERT INTO Inventar_Hat VALUES (1, 3);

                UPDATE Inventar SET gold = gold - 200 WHERE InventarID = 1;
            ";
            transactionCommand.ExecuteNonQuery();


            // die statements oben wurden auch noch nicht in die datenbank geschrieben
            // diesmal kann aber per COMMIT die änderung peristiert werden, da der kauf
            // gültig war
            var commitCommand = db.CreateCommand();
            commitCommand.CommandText = "COMMIT;";
            commitCommand.ExecuteNonQuery();

            // nun hat inventar 1 ein neues item 3 und nur noch 50 gold

            // JOIN STATEMENTS

            var goldAndItemsCommand = db.CreateCommand();
            goldAndItemsCommand.CommandText =
            @"
                SELECT Gold, ItemName
                FROM Inventar_Hat
                INNER JOIN Inventar ON Inventar_Hat.InventarID = Inventar.InventarID
                INNER JOIN Items ON Inventar_Hat.ItemID = Items.ItemID;
            ";

            var resultReader = goldAndItemsCommand.ExecuteReader();
            while (resultReader.Read())
            {
                var gold = resultReader.GetString(0);
                var itemName = resultReader.GetString(1);
                Console.WriteLine($"{gold} \t| {itemName}");
            }


            // AUFRÄUMEN (NUR FÜR DIE DEMO)
            // hier breakpoint setzen oder folgende zeilen auskommentieren, um die datenbank zu inspizieren
            var cleanUpCommand = db.CreateCommand();
            cleanUpCommand = db.CreateCommand();
            cleanUpCommand.CommandText =
            @"
                DROP TABLE Inventar_Hat;
                DROP TABLE Items;
                DROP TABLE Inventar;
            ";
            cleanUpCommand.ExecuteNonQuery();

            db.Close();

        }
    }
}