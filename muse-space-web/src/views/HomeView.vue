<script setup lang="ts"></script>

<template>
  <div class="home">
    <h1 class="text-4xl font-bold text-center mt-10">Welcome to MuseSpace</h1>
    <p class="text-center mt-4 text-gray-500">Vue 3 + TypeScript + Vite</p>
    <div class="flex-center mt-8 gap-4">
      <router-link to="/" class="px-4 py-2 bg-blue-500 text-white rounded hover:bg-blue-600"
        >Home</router-link
      >
      <router-link to="/about" class="px-4 py-2 bg-gray-500 text-white rounded hover:bg-gray-600"
        >About</router-link
      >
    </div>
  </div>
</template>
