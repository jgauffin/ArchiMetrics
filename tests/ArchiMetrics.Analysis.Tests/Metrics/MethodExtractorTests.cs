namespace ArchiMetrics.Analysis.Tests.Metrics
{
    using System.Linq;
    using ArchiMetrics.Analysis.Metrics;
    using Microsoft.CodeAnalysis.CSharp;
    using Xunit;

    public sealed class MethodExtractorTests
    {
        public class GivenMethodExtraction
        {
            [Fact]
            public void WhenExtractingFromTreesThenReturnsExpectedInstances()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Calculate(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100)
                {
                    result = result / 2;
                }
            }
            return result;
        }
    }

    public class B
    {
        public string Format(string input)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var c in input)
            {
                if (char.IsUpper(c))
                {
                    builder.Append(' ');
                }
                builder.Append(char.ToLower(c));
            }
            return builder.ToString().Trim();
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var extractor = new MethodExtractor(string.Empty, minimumTokens: 10);

                var instances = extractor.Extract(new[] { tree });

                Assert.Equal(2, instances.Count);
            }

            [Fact]
            public void WhenMethodBelowTokenThresholdThenSkipped()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Foo() { return 1; }
        public int Bar() { return 2; }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var extractor = new MethodExtractor(string.Empty, minimumTokens: 50);

                var instances = extractor.Extract(new[] { tree });

                Assert.Empty(instances);
            }

            [Fact]
            public void WhenExtractingThenMemberNameIsCorrect()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Calculate(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100)
                {
                    result = result / 2;
                }
            }
            return result;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var extractor = new MethodExtractor(string.Empty, minimumTokens: 10);

                var instances = extractor.Extract(new[] { tree });

                Assert.Single(instances);
                Assert.Equal("Calculate", instances[0].MemberName);
            }

            [Fact]
            public void WhenExtractingThenNormalizedTextIsNotEmpty()
            {
                var code = @"
namespace Test
{
    public class A
    {
        public int Calculate(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100)
                {
                    result = result / 2;
                }
            }
            return result;
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var extractor = new MethodExtractor(string.Empty, minimumTokens: 10);

                var instances = extractor.Extract(new[] { tree });

                Assert.Single(instances);
                Assert.False(string.IsNullOrWhiteSpace(instances[0].NormalizedText));
            }

            [Fact]
            public void WhenExtractingConstructorThenMemberNameIncludesCtor()
            {
                var code = @"
namespace Test
{
    public class A
    {
        private int _value;
        public A(int input)
        {
            _value = input;
            if (input > 0)
            {
                _value = input * 2;
                for (var i = 0; i < input; i++)
                {
                    _value += i;
                }
            }
        }
    }
}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var extractor = new MethodExtractor(string.Empty, minimumTokens: 10);

                var instances = extractor.Extract(new[] { tree });

                Assert.Single(instances);
                Assert.Contains(".ctor", instances[0].MemberName);
            }

            [Fact]
            public void WhenExtractingFromMultipleTreesThenCombinesResults()
            {
                var code1 = @"
namespace Test
{
    public class A
    {
        public int Calculate(int x)
        {
            var result = 0;
            for (var i = 0; i < x; i++)
            {
                result += i * 2;
                if (result > 100) { result = result / 2; }
            }
            return result;
        }
    }
}";
                var code2 = @"
namespace Test
{
    public class B
    {
        public string Format(string input)
        {
            var builder = new System.Text.StringBuilder();
            foreach (var c in input)
            {
                if (char.IsUpper(c)) { builder.Append(' '); }
                builder.Append(char.ToLower(c));
            }
            return builder.ToString().Trim();
        }
    }
}";

                var tree1 = CSharpSyntaxTree.ParseText(code1);
                var tree2 = CSharpSyntaxTree.ParseText(code2);
                var extractor = new MethodExtractor(string.Empty, minimumTokens: 10);

                var instances = extractor.Extract(new[] { tree1, tree2 });

                Assert.Equal(2, instances.Count);
            }
        }
    }
}
