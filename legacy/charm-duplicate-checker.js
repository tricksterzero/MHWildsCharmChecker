#!/usr/bin/env node
/**
 * MHWilds 護石 完全上位互換チェッカー
 *
 * 使い方:
 *   node charm-duplicate-checker.js <CSVパス>
 *
 * 判定ロジック:
 *   護石A が 護石B の「完全上位互換」である条件:
 *     1. スキル構成が完全一致（スキル名の集合が一致し、各スキルのレベルが A ≥ B）
 *     2. 防具スロット3つを降順ソートしたとき、各位置で A ≥ B
 *     3. 武器スロット3つを降順ソートしたとき、各位置で A ≥ B
 *     4. ただし A == B（完全同一）でない（同一の場合は別途「重複」として扱う）
 *
 * 出力:
 *   原本CSVは一切変更しない。
 *   処分候補（下位互換）の行番号と内容をレポート出力する。
 */

'use strict';

const fs = require('fs');
const path = require('path');

// ---------- CSV パース ----------
function parseCsv(text) {
  const lines = text.split(/\r?\n/).filter((l) => l.length > 0);
  return lines.map((line, idx) => {
    const cols = line.split(',');
    if (cols.length !== 12) {
      throw new Error(`行 ${idx + 1}: 列数が12ではありません（${cols.length}列）: ${line}`);
    }
    return cols;
  });
}

// ---------- 護石オブジェクトへ変換 ----------
function rowToCharm(cols, lineNo) {
  // スキル: (名前, レベル) を最大3つ
  const skills = [];
  for (let i = 0; i < 3; i++) {
    const name = cols[i * 2].trim();
    const level = parseInt(cols[i * 2 + 1], 10);
    if (name !== '' && level > 0) {
      skills.push({ name, level });
    }
  }
  // 防具スロット（列7-9 = index 6-8）
  const armorSlots = [
    parseInt(cols[6], 10),
    parseInt(cols[7], 10),
    parseInt(cols[8], 10),
  ];
  // 武器スロット（列10-12 = index 9-11）
  const weaponSlots = [
    parseInt(cols[9], 10),
    parseInt(cols[10], 10),
    parseInt(cols[11], 10),
  ];
  return {
    lineNo,
    raw: cols.join(','),
    skills,
    armorSlots,
    weaponSlots,
  };
}

// ---------- 比較ヘルパ ----------

// スキル構成の正規化キー（順序非依存・完全一致判定用）
function skillSetKey(charm) {
  return charm.skills
    .map((s) => `${s.name}:${s.level}`)
    .sort()
    .join('|');
}

// スキル名の集合キー（レベル無視）
function skillNameSetKey(charm) {
  return charm.skills
    .map((s) => s.name)
    .sort()
    .join('|');
}

// スロット配列を降順ソート
function sortDesc(arr) {
  return [...arr].sort((a, b) => b - a);
}

// A の各スロットが B の各スロット以上か（降順比較）
function slotsGte(a, b) {
  const sa = sortDesc(a);
  const sb = sortDesc(b);
  for (let i = 0; i < sa.length; i++) {
    if (sa[i] < sb[i]) return false;
  }
  return true;
}

// A == B（スロット配列が降順ソート後に完全一致）
function slotsEq(a, b) {
  const sa = sortDesc(a);
  const sb = sortDesc(b);
  for (let i = 0; i < sa.length; i++) {
    if (sa[i] !== sb[i]) return false;
  }
  return true;
}

// 護石 A と B のスキルレベル比較（スキル名集合は一致前提）
// 返り値: 'gt' | 'eq' | 'lt' | 'incomparable'
function compareSkillLevels(a, b) {
  const mapA = new Map(a.skills.map((s) => [s.name, s.level]));
  const mapB = new Map(b.skills.map((s) => [s.name, s.level]));
  // スキル名の集合が違うなら incomparable
  if (mapA.size !== mapB.size) return 'incomparable';
  for (const name of mapA.keys()) {
    if (!mapB.has(name)) return 'incomparable';
  }
  let hasGt = false;
  let hasLt = false;
  for (const [name, lvA] of mapA) {
    const lvB = mapB.get(name);
    if (lvA > lvB) hasGt = true;
    if (lvA < lvB) hasLt = true;
  }
  if (hasGt && !hasLt) return 'gt';
  if (hasLt && !hasGt) return 'lt';
  if (!hasGt && !hasLt) return 'eq';
  return 'incomparable';
}

// 護石 A と B のスロット比較
// 返り値: 'gt' | 'eq' | 'lt' | 'incomparable'
function compareSlots(a, b) {
  const armorAGteB = slotsGte(a.armorSlots, b.armorSlots);
  const armorBGteA = slotsGte(b.armorSlots, a.armorSlots);
  const weaponAGteB = slotsGte(a.weaponSlots, b.weaponSlots);
  const weaponBGteA = slotsGte(b.weaponSlots, a.weaponSlots);

  const aGteB = armorAGteB && weaponAGteB;
  const bGteA = armorBGteA && weaponBGteA;

  if (aGteB && bGteA) return 'eq';
  if (aGteB) return 'gt';
  if (bGteA) return 'lt';
  return 'incomparable';
}

// A が B の完全上位互換か（A > B）
// スキル: eq or gt、スロット: eq or gt、かつ「全 eq」ではない
function isStrictlySuperior(a, b) {
  const skillCmp = compareSkillLevels(a, b);
  if (skillCmp === 'lt' || skillCmp === 'incomparable') return false;
  const slotCmp = compareSlots(a, b);
  if (slotCmp === 'lt' || slotCmp === 'incomparable') return false;
  // 両方 eq なら完全同一なので、ここでは false（同一は別ロジックで処理）
  if (skillCmp === 'eq' && slotCmp === 'eq') return false;
  return true;
}

// A と B が完全同一か
function isIdentical(a, b) {
  const skillCmp = compareSkillLevels(a, b);
  if (skillCmp !== 'eq') return false;
  return slotsEq(a.armorSlots, b.armorSlots) && slotsEq(a.weaponSlots, b.weaponSlots);
}

// ---------- 表示用フォーマット ----------
function formatCharm(charm) {
  const skillStr = charm.skills.map((s) => `${s.name}Lv${s.level}`).join(', ') || '(スキルなし)';
  const armor = sortDesc(charm.armorSlots).filter((x) => x > 0);
  const weapon = sortDesc(charm.weaponSlots).filter((x) => x > 0);
  const slotStr = `防具[${armor.join('/') || '-'}] 武器[${weapon.join('/') || '-'}]`;
  return `${skillStr} / ${slotStr}`;
}

// ---------- メイン処理 ----------
function main() {
  const args = process.argv.slice(2);
  if (args.length === 0) {
    console.error('使い方: node charm-duplicate-checker.js <CSVパス>');
    process.exit(1);
  }
  const csvPath = args[0];
  if (!fs.existsSync(csvPath)) {
    console.error(`ファイルが見つかりません: ${csvPath}`);
    process.exit(1);
  }

  const text = fs.readFileSync(csvPath, 'utf8');
  const rows = parseCsv(text);
  const charms = rows.map((cols, idx) => rowToCharm(cols, idx + 1));

  // --- 1. 完全同一護石をグルーピング（重複） ---
  // 同一護石は1枚を残して残りを処分候補に
  const identicalGroups = new Map(); // key -> [charm, ...]
  for (const c of charms) {
    const key = skillSetKey(c) + '#' +
      sortDesc(c.armorSlots).join(',') + '#' +
      sortDesc(c.weaponSlots).join(',');
    if (!identicalGroups.has(key)) identicalGroups.set(key, []);
    identicalGroups.get(key).push(c);
  }

  const duplicates = []; // 完全同一の重複（2枚目以降）
  for (const group of identicalGroups.values()) {
    if (group.length > 1) {
      // 行番号が若いものを残し、残りを処分候補に
      group.sort((a, b) => a.lineNo - b.lineNo);
      for (let i = 1; i < group.length; i++) {
        duplicates.push({ target: group[i], keep: group[0] });
      }
    }
  }

  // --- 2. 完全上位互換による処分候補 ---
  // 重複として既にマークされた行は対象外
  const duplicateLineNos = new Set(duplicates.map((d) => d.target.lineNo));
  const inferior = []; // {target, superiorList}
  for (const target of charms) {
    if (duplicateLineNos.has(target.lineNo)) continue;
    const superiors = [];
    for (const other of charms) {
      if (other.lineNo === target.lineNo) continue;
      if (duplicateLineNos.has(other.lineNo)) continue;
      if (isStrictlySuperior(other, target)) {
        superiors.push(other);
      }
    }
    if (superiors.length > 0) {
      inferior.push({ target, superiors });
    }
  }

  // --- 出力 ---
  const sep = '='.repeat(70);
  console.log(sep);
  console.log(`護石上位互換チェッカー / 入力: ${path.basename(csvPath)}`);
  console.log(`総件数: ${charms.length}`);
  console.log(sep);
  console.log();

  console.log('【1】完全同一の重複');
  console.log('-'.repeat(70));
  if (duplicates.length === 0) {
    console.log('完全に同一な護石の重複はありません。');
  } else {
    for (const d of duplicates) {
      console.log(`行 ${d.target.lineNo}: ${formatCharm(d.target)}`);
      console.log(`  → 行 ${d.keep.lineNo} と完全同一（こちらを残す）`);
    }
    console.log();
    console.log(`重複による処分候補: ${duplicates.length} 件`);
  }
  console.log();

  console.log('【2】完全上位互換あり（下位互換のため処分候補）');
  console.log('-'.repeat(70));
  if (inferior.length === 0) {
    console.log('完全上位互換による処分候補はありません。');
  } else {
    for (const item of inferior) {
      console.log(`行 ${item.target.lineNo}: ${formatCharm(item.target)}`);
      console.log(`  上位互換が存在:`);
      for (const sup of item.superiors) {
        console.log(`    行 ${sup.lineNo} が上位互換: ${formatCharm(sup)}`);
        console.log();
      }
    }
    console.log();
    console.log(`上位互換存在による処分候補: ${inferior.length} 件`);
  }
  console.log();

  console.log(sep);
  console.log(`処分候補 合計: ${duplicates.length + inferior.length} 件 / ${charms.length} 件中`);
  console.log(sep);
}

main();
