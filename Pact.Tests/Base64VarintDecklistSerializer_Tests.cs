﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Pact.Tests
{
    public sealed class Base64VarintDecklistSerializer_Tests
    {
        [Fact]
        public async void Serialize_UsingDeserializedDecklist_ProducesOriginalDeckstring()
        {
            string deckstring = "AAECAaIHCLICrwSoBfIFkbwCm8sCz+ECnOICC7QB7QLUBd0IkrYCgcIC68ICyssCps4C+9MC2+MCAA==";

            ISerializer<Models.Client.Decklist> serializer =
                new TextDecklistSerializer(
                    new VarintDecklistSerializer(
                        new LocalDatabaseCardInfoProvider(
                            new JSONCardDatabase(@"C:\Users\Nicholas Anderson\Documents\Visual Studio 2017\Projects\Pact\cards.json"),
                            ((Valkyrie.IEventDispatcherFactory)new Valkyrie.InMemoryEventDispatcherFactory()).Create())));

            using (var inputStream = new MemoryStream(Encoding.Default.GetBytes(deckstring)))
            {
                Models.Client.Decklist decklist = await serializer.Deserialize(inputStream);

                using (var outputStream = new MemoryStream())
                {
                    await serializer.Serialize(outputStream, decklist);

                    outputStream.Position = 0;

                    using (var reader = new StreamReader(outputStream))
                        Assert.Equal(deckstring, reader.ReadToEnd());
                }
            }
        }

        [Fact]
        public async void LatestVersion_Test()
        {
            ICardDatabaseUpdateService test = new HearthstoneJSONCardDatabaseUpdateService("https://api.hearthstonejson.com/v1/");

            Console.WriteLine(await test.GetLatestVersion());
        }
    }
}
