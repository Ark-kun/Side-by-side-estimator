using System;
using System.Collections.Generic;
using System.IO;

namespace Ark {
    namespace Text {
        public enum LineEndingStyle {
            Unknown,
            CR,
            LF,
            CRLF
        }

        public static class TextExtensions {
            public static IEnumerable<string> ReadLines(this TextReader reader) {
                if (reader == null) {
                    throw new ArgumentNullException("reader");
                }

                string line;
                while ((line = reader.ReadLine()) != null) {
                    yield return line;
                }
            }

            public static IEnumerable<string> ReadLinesThenNull(this TextReader reader) {
                if (reader == null) {
                    throw new ArgumentNullException("reader");
                }

                string line;
                while ((line = reader.ReadLine()) != null) {
                    yield return line;
                }
                yield return null;
            }

            public static Text.LineEndingStyle DetectLineEndingOfCurrentLine(this TextReader reader) {
                if (reader == null) {
                    throw new ArgumentNullException("reader");
                }

                int c;
                while ((c = reader.Read()) != -1) {
                    if (c == '\n') {
                        return Text.LineEndingStyle.LF;
                    }
                    if (c == '\r') {
                        if (reader.Read() == '\n') {
                            return Text.LineEndingStyle.CRLF;
                        } else {
                            return Text.LineEndingStyle.CR;
                        }
                    }
                }
                return Text.LineEndingStyle.Unknown;
            }
        }
    }
}