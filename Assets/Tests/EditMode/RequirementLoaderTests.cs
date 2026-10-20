using System;
using System.IO;
using Core.Requirements;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class RequirementLoaderTests
    {
        [Test]
        public void LoadFromJson_ValidJsonWithoutMaxClasses_UsesDefaultFallbackOfSeven()
        {
            const string sampleJson = @"{
                ""Biblioteca"": [
                    {
                        ""Id"": ""trail_01"",
                        ""RequirementsByTurn"": [
                            { ""Id"": ""req_1"", ""Description"": ""T1"", ""RequiredMethods"": [ { ""Name"": ""M1"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_2"", ""Description"": ""T2"", ""RequiredMethods"": [ { ""Name"": ""M2"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_3"", ""Description"": ""T3"", ""RequiredMethods"": [ { ""Name"": ""M3"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_4"", ""Description"": ""T4"", ""RequiredMethods"": [ { ""Name"": ""M4"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_5"", ""Description"": ""T5"", ""RequiredMethods"": [ { ""Name"": ""M5"", ""Category"": ""Usuario"" } ] }
                        ]
                    }
                ]
            }";

            var result = RequirementLoader.LoadFromJson(sampleJson);

            Assert.IsTrue(result.ContainsKey("Biblioteca"));
            var trails = result["Biblioteca"];
            Assert.AreEqual(1, trails.Count);
            Assert.AreEqual("trail_01", trails[0].Id);
            Assert.AreEqual(Trail.DefaultMaxClasses, trails[0].MaxClasses);
            Assert.AreEqual(7, trails[0].MaxClasses);
            Assert.AreEqual(5, trails[0].RequirementByTurn.Count);
            Assert.AreEqual("req_1", trails[0].RequirementByTurn[0].Id);
            Assert.AreEqual(1, trails[0].RequirementByTurn[0].RequirementMethods.Count);
            Assert.AreEqual("M1", trails[0].RequirementByTurn[0].RequirementMethods[0].Name);
            Assert.AreEqual("Usuario", trails[0].RequirementByTurn[0].RequirementMethods[0].Category);
        }

        [Test]
        public void LoadFromJson_WithInvalidMaxClasses_ThrowsFormatException()
        {
            const string invalidJson = @"{
                ""Biblioteca"": [
                    {
                        ""Id"": ""trail_01"",
                        ""MaxClasses"": 0,
                        ""RequirementsByTurn"": [
                            { ""Id"": ""req_1"", ""Description"": ""T1"", ""RequiredMethods"": [ { ""Name"": ""M1"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_2"", ""Description"": ""T2"", ""RequiredMethods"": [ { ""Name"": ""M2"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_3"", ""Description"": ""T3"", ""RequiredMethods"": [ { ""Name"": ""M3"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_4"", ""Description"": ""T4"", ""RequiredMethods"": [ { ""Name"": ""M4"", ""Category"": ""Usuario"" } ] },
                            { ""Id"": ""req_5"", ""Description"": ""T5"", ""RequiredMethods"": [ { ""Name"": ""M5"", ""Category"": ""Usuario"" } ] }
                        ]
                    }
                ]
            }";

            Assert.Throws<FormatException>(() => RequirementLoader.LoadFromJson(invalidJson));
        }

        [Test]
        public void LoadFromJson_ProjectRequirementsFile_LoadsFiveTurnsAndMaxClassesSuccessfully()
        {
            string path = Path.Combine(Application.dataPath, "Resources", "requirements.json");
            Assert.IsTrue(File.Exists(path), $"Arquivo requirements.json não encontrado em: {path}");

            string jsonContent = File.ReadAllText(path);
            var result = RequirementLoader.LoadFromJson(jsonContent);

            Assert.IsTrue(result.ContainsKey("Biblioteca"), "O tema 'Biblioteca' deve estar presente no JSON.");
            var trails = result["Biblioteca"];
            Assert.GreaterOrEqual(trails.Count, 1, "Deve existir ao menos uma trilha para 'Biblioteca'.");

            var firstTrail = trails[0];
            Assert.AreEqual(7, firstTrail.MaxClasses, "A trilha de teste deve ter MaxClasses = 7.");
            Assert.AreEqual(5, firstTrail.RequirementByTurn.Count, "A trilha piloto deve conter exatamente 5 turnos de requisitos.");

            for (int i = 0; i < 5; i++)
            {
                var req = firstTrail.RequirementByTurn[i];
                Assert.IsFalse(string.IsNullOrWhiteSpace(req.Id), $"Requisito do turno {i + 1} deve ter um ID.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(req.Description), $"Requisito do turno {i + 1} deve ter uma descrição.");
                Assert.Greater(req.RequirementMethods.Count, 0, $"Requisito do turno {i + 1} deve exigir ao menos um método.");
            }
        }
    }
}
