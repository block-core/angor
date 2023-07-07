using Angor.Shared;

namespace Angor.Test
{
    public class ScriptThresholdTest
    {
        [Fact]
        public void TestCreateThresholds_2_ABC()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C" };
            var threshold = 2;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(3, result.Count); // 3 choose 2 = 3
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            var expectedCombinations = new List<List<string>>
            {
                new List<string> { "A", "B" },
                new List<string> { "A", "C" },
                new List<string> { "B", "C" }
            };

            foreach (var expectedCombination in expectedCombinations)
            {
                Assert.Contains(result, combination => combination.SequenceEqual(expectedCombination));
            }
        }

        [Fact]
        public void TestCreateThresholds_3_ABCDE()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C", "D", "E" };
            var threshold = 3;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(10, result.Count); // 5 choose 3 = 10
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            var expectedCombinations = new List<List<string>>
            {
                new List<string> { "A", "B", "C" },
                new List<string> { "A", "B", "D" },
                new List<string> { "A", "B", "E" },
                new List<string> { "A", "C", "D" },
                new List<string> { "A", "C", "E" },
                new List<string> { "A", "D", "E" },
                new List<string> { "B", "C", "D" },
                new List<string> { "B", "C", "E" },
                new List<string> { "B", "D", "E" },
                new List<string> { "C", "D", "E" }
            };

            foreach (var expectedCombination in expectedCombinations)
            {
                Assert.Contains(result, combination => combination.SequenceEqual(expectedCombination));
            }
        }

        [Fact]
        public void TestCreateThresholds_4_ABCDE()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C", "D", "E" };
            var threshold = 4;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(5, result.Count); // 5 choose 4 = 5
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            var expectedCombinations = new List<List<string>>
            {
                new List<string> { "A", "B", "C", "D" },
                new List<string> { "A", "B", "C", "E" },
                new List<string> { "A", "B", "D", "E" },
                new List<string> { "A", "C", "D", "E" },
                new List<string> { "B", "C", "D", "E" }
            };

            foreach (var expectedCombination in expectedCombinations)
            {
                Assert.Contains(result, combination => combination.SequenceEqual(expectedCombination));
            }
        }


        [Fact]
        public void TestCreateThresholds_4_ABCDEF()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C", "D", "E", "F" };
            var threshold = 4;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(15, result.Count); // 6 choose 4 = 15
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            var expectedCombinations = new List<List<string>>
            {
                new List<string> { "A", "B", "C", "D" },
                new List<string> { "A", "B", "C", "E" },
                new List<string> { "A", "B", "C", "F" },
                new List<string> { "A", "B", "D", "E" },
                new List<string> { "A", "B", "D", "F" },
                new List<string> { "A", "B", "E", "F" },
                new List<string> { "A", "C", "D", "E" },
                new List<string> { "A", "C", "D", "F" },
                new List<string> { "A", "C", "E", "F" },
                new List<string> { "A", "D", "E", "F" },
                new List<string> { "B", "C", "D", "E" },
                new List<string> { "B", "C", "D", "F" },
                new List<string> { "B", "C", "E", "F" },
                new List<string> { "B", "D", "E", "F" },
                new List<string> { "C", "D", "E", "F" }
            };

            foreach (var expectedCombination in expectedCombinations)
            {
                Assert.Contains(result, combination => combination.SequenceEqual(expectedCombination));
            }
        }

        [Fact]
        public void TestCreateThresholds_3_ABCDEF()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C", "D", "E", "F" };
            var threshold = 3;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(20, result.Count); // 6 choose 3 = 20
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            var expectedCombinations = new List<List<string>>
            {
                new List<string> { "A", "B", "C" },
                new List<string> { "A", "B", "D" },
                new List<string> { "A", "B", "E" },
                new List<string> { "A", "B", "F" },
                new List<string> { "A", "C", "D" },
                new List<string> { "A", "C", "E" },
                new List<string> { "A", "C", "F" },
                new List<string> { "A", "D", "E" },
                new List<string> { "A", "D", "F" },
                new List<string> { "A", "E", "F" },
                new List<string> { "B", "C", "D" },
                new List<string> { "B", "C", "E" },
                new List<string> { "B", "C", "F" },
                new List<string> { "B", "D", "E" },
                new List<string> { "B", "D", "F" },
                new List<string> { "B", "E", "F" },
                new List<string> { "C", "D", "E" },
                new List<string> { "C", "D", "F" },
                new List<string> { "C", "E", "F" },
                new List<string> { "D", "E", "F" }
            };

            foreach (var expectedCombination in expectedCombinations)
            {
                Assert.Contains(result, combination => combination.SequenceEqual(expectedCombination));
            }
        }

        [Fact]
        public void TestCreateThresholds_4_ABCDEFG()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C", "D", "E", "F", "G" };
            var threshold = 4;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(35, result.Count); // 7 choose 4 = 35
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            // Due to the large number of combinations, it's not practical to list them all here.
            // Instead, we'll just check that the count is correct and that all sublists have the correct length.
        }

        [Fact]
        public void TestCreateThresholds_5_ABCDEFG()
        {
            // Arrange
            var secretHashes = new List<string> { "A", "B", "C", "D", "E", "F", "G" };
            var threshold = 5;

            // Act
            var result = ScriptBuilder.CreateThresholds(threshold, secretHashes);

            // Assert
            Assert.Equal(21, result.Count); // 7 choose 5 = 21
            Assert.True(result.All(subList => subList.Count == threshold)); // All sublists have count equal to threshold

            // Assert each combination
            // Due to the large number of combinations, it's not practical to list them all here.
            // Instead, we'll just check that the count is correct and that all sublists have the correct length.
        }
    }
}
