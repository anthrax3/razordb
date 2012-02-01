﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;

namespace RazorDB {
    
    public class KeyValueStore : IDisposable {

        public KeyValueStore(string baseFileName) {
            _baseFileName = baseFileName;
            string directoryName = Path.GetDirectoryName(baseFileName);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);

            _currentJournaledMemTable = new JournaledMemTable(_baseFileName, _level_0_version);
        }

        ~KeyValueStore() {
            Dispose();
        }

        private string _baseFileName;
        private JournaledMemTable _currentJournaledMemTable;
        private int _level_0_version = 0;

        public void Set(byte[] key, byte[] value) {
            var k = new ByteArray(key);
            var v = new ByteArray(value);
            _currentJournaledMemTable.Add(k, v);
            if (_currentJournaledMemTable.Full) {
                RotateMemTable();
            }
        }

        public byte[] Get(byte[] key) {
            ByteArray output;
            if (_currentJournaledMemTable.Lookup(new ByteArray(key), out output)) {
                return output.InternalBytes;
            } else {
                return null;
            }
        }

        private object memTableRotationLock = new object();

        public void RotateMemTable() {
            lock (memTableRotationLock) {
                // Double check the flag in case we have multiple threads that make it into this routine
                if (_currentJournaledMemTable.Full) {
                    _level_0_version++;
                    var oldMemTable = Interlocked.Exchange<JournaledMemTable>(ref _currentJournaledMemTable, new JournaledMemTable(_baseFileName, _level_0_version));
                    oldMemTable.AsyncWriteToSortedBlockTable();
                }
            }
        }

        public void Dispose() {
            Close();
        }

        public void Close() {
            if (_currentJournaledMemTable != null) {
                _currentJournaledMemTable.Close();
                _currentJournaledMemTable = null;
            }
        }
    }

}
