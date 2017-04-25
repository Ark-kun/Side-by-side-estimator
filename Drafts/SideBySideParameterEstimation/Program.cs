using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ark.Collections;
using Ark.Text;
using System.IO;
using System.Diagnostics;

namespace SideBySideParameterEstimation {
    class LearnableParameter {
        public double Value { get; set; }
        public double Gradient { get; set; }

        public void ClearGradient() {
            Gradient = 0;
        }

        public void Update(double learnRate) {
            Value += Gradient * learnRate;
        }

        static Random rng = new Random(13);

        public static LearnableParameter CreateRandom(double min = -1, double max = 1) {
            return new LearnableParameter() { Value = min + (max - min) * rng.NextDouble() };
        }

        public override string ToString() {
            return Value.ToString();
        }
    }

    //public class NormalDistribution {
    //    public double Mean { get; set; }
    //    public double Variance { get; set; }

    //    public double PdfAt(double x) {
    //        return Math.Exp(-(x - Mean)* (x - Mean)/(2*Variance)) / Math.Sqrt(2 * Math.PI * Variance);
    //    }

    //    public double CdfAt(double x) {
    //        return 0.5*(1 + ErrorFunction)
    //    }
    //}

    class Program {
        static void Main(string[] args) {
            var intervalIndicesForJudgements = new Dictionary<string, int>() {
                { "left", 0 },
                { "equal", 1 },
                { "right", 2 }
            };

            var queries = new HashSet<string>();
            var engines = new HashSet<string>();
            var itemIndices = new Dictionary<string, int>();

            var trainingData = new Dictionary<Tuple<int, int, int>, int>();

            var trainingFile = @"data.tsv";
            using (var reader = new StreamReader(trainingFile)) {
                var header = reader.ReadLine().Split('\t').ToIndexDictionary();
                var judgementColumnIdx = header["judgement"];
                var queryColumnIdx = header["query"];
                var leftEngineColumnIdx = header["leftEngine"];
                var rightEngineColumnIdx = header["rightEngine"];
                var isFlippedColumnIdx = header["isFlipped"];

                foreach (var line in reader.ReadLines()) {
                    var row = line.Split('\t');
                    var judgementIdx = intervalIndicesForJudgements.GetValueOrDefault(row[judgementColumnIdx], -1);
                    if (judgementIdx == -1) {
                        continue;
                    }
                    bool isFlipped = bool.Parse(row[isFlippedColumnIdx]);
                    var leftEngine = row[leftEngineColumnIdx];
                    var rightEngine = row[rightEngineColumnIdx];
                    if (isFlipped) {
                        (leftEngine, rightEngine) = (rightEngine, leftEngine);
                    }
                    var query = row[queryColumnIdx];
                    queries.Add(query);
                    engines.Add(leftEngine);
                    engines.Add(rightEngine);

                    var plusItemKey = query + "@" + rightEngine;
                    var minusItemKey = query + "@" + leftEngine;

                    int plusItemIdx = itemIndices.GetOrAddToIndexDictionary(plusItemKey);
                    int minusItemIdx = itemIndices.GetOrAddToIndexDictionary(minusItemKey);
                    var dataKey = Tuple.Create(plusItemIdx, minusItemIdx, judgementIdx);
                    trainingData.IncreaseNumberForKeyInMultiset(dataKey);
                }
            }

            var itemCount = itemIndices.Count;
            var itemMeans = Enumerable.Range(0, itemCount).Select(_ => new LearnableParameter()).ToList();
            //var itemStDevs = Enumerable.Range(0, itemCount).Select(_ => LearnableParameter.CreateRandom()).ToList();
            //var itemStDevs = Enumerable.Range(0, itemCount).Select(_ => LearnableParameter.CreateRandom(1, 2)).ToList();
            var itemStDevs = Enumerable.Range(0, itemCount).Select(_ => new LearnableParameter { Value = 1 }).ToList();

            var intervalsCount = intervalIndicesForJudgements.Count;
            var boundaries = Enumerable.Range(0, intervalsCount - 1).Select(idx => new LearnableParameter() { Value = idx }).ToList();

            double learnRate = 0.0001;
            int iterationCount = 10000;

            for (int i = 0; i < iterationCount; ++i) {
                double logProbability = 0;
                itemMeans.ForEach(x => x.ClearGradient());
                itemStDevs.ForEach(x => x.ClearGradient());
                boundaries.ForEach(x => x.ClearGradient());
                foreach (var kv in trainingData) {
                    var (plusItemIdx, minusItemIdx, judgementIdx) = kv.Key;
                    var outcomesCount = kv.Value;

                    double deltaMean = itemMeans[plusItemIdx].Value - itemMeans[minusItemIdx].Value;
                    double deltaVariance = itemStDevs[plusItemIdx].Value * itemStDevs[plusItemIdx].Value + itemStDevs[minusItemIdx].Value * itemStDevs[minusItemIdx].Value;
                    double deltaStDev = Math.Sqrt(deltaVariance);
                    var rightEqualsLeftProbablilityDensity = MathNet.Numerics.Distributions.Normal.PDF(deltaMean, deltaStDev, 0);
                    Debug.Assert(!double.IsNaN(rightEqualsLeftProbablilityDensity));

                    var leftBoundary = judgementIdx > 0 ? boundaries[judgementIdx - 1] : null;
                    var rightBoundary = judgementIdx < boundaries.Count ? boundaries[judgementIdx] : null;

                    //Prob(a<x<b) = 0.5 * erf((b-Mean1)/(sqrt(2*Var1)))] - 0.5 * erf((a-Mean1)/(sqrt(2*Var1)))]
                    double rightBoundaryValue = rightBoundary?.Value ?? double.PositiveInfinity;
                    double leftBoundaryValue = leftBoundary?.Value ?? double.NegativeInfinity;
                    Debug.Assert(rightBoundaryValue > leftBoundaryValue);
                    double outcomeProbability =
                        (rightBoundary == null ? 1 : 0.5 * (1 + MathNet.Numerics.SpecialFunctions.Erf((rightBoundary.Value - deltaMean) / (Math.Sqrt(2 * deltaVariance)))))
                        -
                        (leftBoundary == null ? 0 : 0.5 * (1 + MathNet.Numerics.SpecialFunctions.Erf((leftBoundary.Value - deltaMean) / (Math.Sqrt(2 * deltaVariance)))))
                    ;
                    Debug.Assert(outcomeProbability >= 0 && outcomeProbability <= 1);
                    Debug.Assert(outcomeProbability > 0);
                    logProbability += outcomesCount * Math.Log(outcomeProbability);
                    double gradientCoeff = outcomesCount / outcomeProbability;

                    //left boundary
                    if (leftBoundary != null) {
                        leftBoundary.Gradient += gradientCoeff * -rightEqualsLeftProbablilityDensity; //Fix: Divide by outcome probability

                        var rightMinusLeftProbablilityDensityAtLeftBoundary = MathNet.Numerics.Distributions.Normal.PDF(deltaMean, deltaStDev, leftBoundary.Value);
                        Debug.Assert(!double.IsNaN(rightMinusLeftProbablilityDensityAtLeftBoundary));

                        itemMeans[plusItemIdx].Gradient += gradientCoeff * +rightMinusLeftProbablilityDensityAtLeftBoundary; //Fix: Divide by outcome probability
                        itemMeans[minusItemIdx].Gradient += gradientCoeff * -rightMinusLeftProbablilityDensityAtLeftBoundary; //Fix: Divide by outcome probability

                        itemStDevs[plusItemIdx].Gradient += gradientCoeff * +rightMinusLeftProbablilityDensityAtLeftBoundary * (leftBoundary.Value - deltaMean) / (deltaVariance); //Fix: Divide by outcome probability
                        itemStDevs[minusItemIdx].Gradient += gradientCoeff * +rightMinusLeftProbablilityDensityAtLeftBoundary * (leftBoundary.Value - deltaMean) / (deltaVariance); //Fix: Divide by outcome probability
                    }

                    //right boundary
                    if (rightBoundary != null) {
                        rightBoundary.Gradient += gradientCoeff * +rightEqualsLeftProbablilityDensity; //Fix: Divide by outcome probability

                        var rightMinusLeftProbablilityDensityAtRightBoundary = MathNet.Numerics.Distributions.Normal.PDF(deltaMean, deltaStDev, rightBoundary.Value);
                        Debug.Assert(!double.IsNaN(rightMinusLeftProbablilityDensityAtRightBoundary));

                        itemMeans[plusItemIdx].Gradient += gradientCoeff * -rightMinusLeftProbablilityDensityAtRightBoundary; //Fix: Divide by outcome probability
                        itemMeans[minusItemIdx].Gradient += gradientCoeff * +rightMinusLeftProbablilityDensityAtRightBoundary; //Fix: Divide by outcome probability

                        itemStDevs[plusItemIdx].Gradient += gradientCoeff * -rightMinusLeftProbablilityDensityAtRightBoundary * (rightBoundary.Value - deltaMean) / (deltaVariance); //Fix: Divide by outcome probability
                        itemStDevs[minusItemIdx].Gradient += gradientCoeff * -rightMinusLeftProbablilityDensityAtRightBoundary * (rightBoundary.Value - deltaMean) / (deltaVariance); //Fix: Divide by outcome probability
                    }
                }
                Console.WriteLine($"Iteration: {i}\tln(Probability) = {logProbability}");

                itemMeans.ForEach(x => x.Update(learnRate));
                itemStDevs.ForEach(x => x.Update(learnRate));
                boundaries.ForEach(x => x.Update(learnRate));

                //StDev of pair items is always the same as the gradients are always the same. Means are the opposite. This is normal when you only observe A-B and B-A
            }
            File.WriteAllLines("boundaries.txt", boundaries.Select(b => b.Value.ToString()));
            File.WriteAllLines("queryResults.txt", itemIndices.Select(kv => string.Join("\t", kv.Key, itemMeans[kv.Value].Value, itemStDevs[kv.Value].Value)));

            //double normCoeff = boundaries.Last().Value > 0 ? boundaries.Last().Value : Math.Abs(boundaries.First().Value);
            double normCoeff = Math.Max(Math.Abs(boundaries.First().Value), Math.Abs(boundaries.Last().Value));
            File.WriteAllLines("boundaries.norm.txt", boundaries.Select(b => (b.Value / normCoeff).ToString()));
            File.WriteAllLines("queryResults.norm.txt", itemIndices.Select(kv => string.Join("\t", kv.Key, itemMeans[kv.Value].Value / normCoeff, itemStDevs[kv.Value].Value / normCoeff)));

            if (engines.Count == 2) {
                var orderedFlights = engines.OrderBy(s => s).ToList();
                File.WriteAllLines("enginesOrder.txt", orderedFlights);
                File.WriteAllLines("queryDeltas.txt", queries.Select(query => {
                    var key1 = query + "@" + orderedFlights.First();
                    var key2 = query + "@" + orderedFlights.Last();
                    var deltaMean = itemMeans[itemIndices[key1]].Value - itemMeans[itemIndices[key2]].Value;
                    var deltaVariance = itemStDevs[itemIndices[key1]].Value * itemStDevs[itemIndices[key1]].Value + itemStDevs[itemIndices[key2]].Value * itemStDevs[itemIndices[key2]].Value;
                    var deltaStDev = Math.Sqrt(deltaVariance);
                    return string.Join("\t", query, deltaMean, deltaStDev);
                }));
                File.WriteAllLines("queryDeltas.norm.txt", queries.Select(query => {
                    var key1 = query + "@" + orderedFlights.First();
                    var key2 = query + "@" + orderedFlights.Last();
                    var deltaMean = itemMeans[itemIndices[key1]].Value - itemMeans[itemIndices[key2]].Value;
                    var deltaVariance = itemStDevs[itemIndices[key1]].Value * itemStDevs[itemIndices[key1]].Value + itemStDevs[itemIndices[key2]].Value * itemStDevs[itemIndices[key2]].Value;
                    var deltaStDev = Math.Sqrt(deltaVariance);
                    return string.Join("\t", query, deltaMean / normCoeff, deltaStDev / normCoeff);
                }));

            } else {
                throw new InvalidOperationException();
            }
        }

        public struct TrainingDataRow {

        }
    }
}
